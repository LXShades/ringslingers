using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameTicker : NetworkBehaviour
{
    public static GameTicker singleton { get; private set; }

    /// <summary>
    /// Combined tick from server to clients indicating current game and player state
    /// </summary>
    private struct ServerTickMessage : NetworkMessage
    {
        public float serverTime; // time of the server
        public float confirmedClientTime; // time of the receiving client, as last seen on the server
        public float extrapolatedClientTime; // time of the receiving client, as last seen on the server, with server extrapolation included for smoothness
        public ArraySegment<ServerPlayerTick> ticks;
    }

    /// <summary>
    /// Per-player tick from server to clients indicating a player's current state
    /// </summary>
    private struct ServerPlayerTick
    {
        public byte id;
        public MoveStateWithInput moveState;
        public byte sounds;
    }

    /// <summary>
    /// A player input message send from clients to server
    /// </summary>
    private struct ClientPlayerInput : NetworkMessage
    {
        public InputPack inputPack;
    }

    // Client/server flow control settings for better network smoothing
    [Header("Flow control")]
    public FlowControlSettings serverFlowControlSettings = FlowControlSettings.Default;
    public FlowControlSettings clientFlowControlSettings = FlowControlSettings.Default;

    [Header("Prediction")]
    [Range(0.1f, 1f), Tooltip("[client] How far ahead the player can predict")]
    public float pingTolerance = 1f;
    [Range(0.01f, 1f), Tooltip("[client] The length of buffered inputs to send to the server for safety, in seconds. Shorter means lower net traffic but possibly more missed inputs under bad net conditions")]
    public float sendBufferLength = 0.2f;

    [Space, Tooltip("Whether to extrapolate the local character's movement")]
    public bool extrapolateLocalInput = true;

    [Space, Tooltip("Maximum delta time allowed between game ticks")]
    public float maxDeltaTime = 0.5f;

    [Header("Remote playback")]
    [Tooltip("[client] Whether to extrapolate remote clients' movement based on their last input.")]
    public bool extrapolateRemoteInput = true;
    [Range(0f, 1f), Tooltip("[client] How far ahead we can predict other players' movements, maximally")]
    public float maxRemotePrediction = 0.3f;
    [Range(0.01f, 0.5f), Tooltip("[client] When predicting other players, this is the tick rate to tick them by, expressed in second intervals. Should be small enough for accuracy but high enough to avoid spending too much processing time on them." +
        "Recommended around 0.0666 for 15hz")]
    public float remotePredictionTimeStep = 0.08f;

    [Header("Reconcilation")]
    [Tooltip("[client] Whether to reconcile even if the server agreed position matches our own")]
    public bool alwaysReconcile = false;
    [Tooltip("[client] How far can our position differ from the server's until we must be corrected, in metres.")]
    public float reconcilationPositionTolerance = 0.3f;

    // preallocated outgoing player ticks
    private readonly List<ServerPlayerTick> ticksOut = new List<ServerPlayerTick>(32);

    // [server] Incoming input flow controller per player
    private readonly Dictionary<int, NetFlowController<InputPack>> playerInputFlow = new Dictionary<int, NetFlowController<InputPack>>();
    // [client] Incoming server tick flow
    private readonly NetFlowController<ServerTickMessage> serverTickFlow = new NetFlowController<ServerTickMessage>();

    // ================== TIMING SECTION ================
    // [client] what server time they are predicting into [server] actual server time
    // client predicted server time will be extrapolated based on ping and prediction settings
    public float predictedServerTime { get; private set; }

    // ==================== LOCAL PLAYER SECTION ===================
    // [client] returns the local player's ping
    public float localPlayerPing { get; private set; }

    // [client/server] returns the latest local player input this tick
    public PlayerInput localPlayerInput;

    private void Awake()
    {
        singleton = this;

        NetworkClient.RegisterHandler<ServerTickMessage>(OnRecvServerPlayerTick);
        NetworkServer.RegisterHandler<ClientPlayerInput>(OnRecvClientInput);

        serverTickFlow.flowControlSettings = serverFlowControlSettings;
    }

    private void Update()
    {
        // Advance the clock
        if (NetworkServer.active)
            predictedServerTime = Time.realtimeSinceStartup; // we technically don't run a prediction of the server time on the server
        else
            predictedServerTime += Time.unscaledDeltaTime; // we just tick it up and update properly occasionally

        // Receive incoming messages
        ReceiveIncomings();

        // Add states, etc to our local prediction history
        if (Netplay.singleton.localPlayer)
        {
            // Receive local player inputs
            Vector3 localPlayerUp = Netplay.singleton.localPlayer.GetComponent<CharacterMovement>().up;

            localPlayerInput = PlayerInput.MakeLocalInput(localPlayerInput, localPlayerUp);

            // Send inputs to the local player's ticker
            Netplay.singleton.localPlayer.ticker.PushInput(localPlayerInput, Time.time);
        }

        // We have all server inputs and our own inputs, tick the game
        TickGame();

        // Client/server send messages
        SendFinalOutgoings();
    }

    /// <summary>
    /// Receives incoming messages
    /// </summary>
    private void ReceiveIncomings()
    {
        // Client receive server ticks if available
        if (serverTickFlow.TryPopMessage(out ServerTickMessage serverTick, true))
            ApplyServerTick(serverTick);

        // Server receive inputs from players
        for (int i = 0; i < Netplay.singleton.players.Count; i++)
        {
            Character character = Netplay.singleton.players[i];

            if (character && playerInputFlow.ContainsKey(i))
            {
                while (playerInputFlow[i].TryPopMessage(out InputPack inputPack, false)) // skipOutdatedMessages is false because we'd like to receive everything we got since the last one
                    character.ticker.PushInputPack(inputPack);
            }
        }
    }

    /// <summary>
    /// Ticks the predictable elements of the game and players
    /// </summary>
    private void TickGame()
    {
        // Tick all players
        foreach (Character player in Netplay.singleton.players)
        {
            if (player)
            {
                if (player == Netplay.singleton.localPlayer)
                    player.ticker.Seek(Time.time, isServer ? player.ticker.confirmedPlaybackTime : player.ticker.playbackTime);
                else if (isServer)
                    player.ticker.Seek(player.ticker.inputHistory.LatestTime + Time.time - player.ticker.timeOfLastInputPush, player.ticker.confirmedPlaybackTime);
                else if (isClient)
                    player.ticker.Seek(predictedServerTime, player.ticker.playbackTime);
            }
        }


    }

    /// <summary>
    /// Sends final outgoing messages
    /// </summary>
    private void SendFinalOutgoings()
    {
        if (Netplay.singleton.isPlayerTick)
        {
            if (NetworkServer.active)
            {
                // Send server tick to all players (lots of stuff happening here!)
                ServerTickMessage tick;
                
                tick = MakeTickMessage();

                for (int i = 0; i < tick.ticks.Count; i++)
                {
                    Character player = Netplay.singleton.players[tick.ticks.Array[tick.ticks.Offset + i].id];
                    tick.extrapolatedClientTime = player.GetComponent<Ticker>().playbackTime;
                    tick.confirmedClientTime = player.GetComponent<Ticker>().confirmedPlaybackTime;
                    player.netIdentity.connectionToClient.Send(tick, Channels.Unreliable);
                }
                NetworkServer.SendToAll(tick, Channels.Unreliable, true);
            }
            else if (NetworkClient.isConnected)
            {
                // Send local player inputs to server
                Ticker localTicker = Netplay.singleton.localPlayer != null ? Netplay.singleton.localPlayer.GetComponent<Ticker>() : null;

                if (localTicker)
                    NetworkClient.Send(new ClientPlayerInput() { inputPack = localTicker.MakeInputPack(sendBufferLength) }, Channels.Unreliable);
            }
        }
    }

    /// <summary>
    /// Makes a server tick from the game state
    /// </summary>
    private ServerTickMessage MakeTickMessage()
    {
        ticksOut.Clear();

        for (int i = 0; i < Netplay.singleton.players.Count; i++)
        {
            Character character = Netplay.singleton.players[i];

            if (character)
            {
                PlayerSounds sounds = character.GetComponent<PlayerSounds>();
                Ticker ticker = character.GetComponent<Ticker>();

                ticksOut.Add(new ServerPlayerTick()
                {
                    id = (byte)i,
                    sounds = sounds.soundHistory,
                    moveState = new MoveStateWithInput()
                    {
                        state = ticker.lastConfirmedState,
                        input = ticker.inputHistory.Latest
                    }
                });
            }
        }

        ServerTickMessage tick = new ServerTickMessage()
        {
            serverTime = predictedServerTime,
            ticks = new ArraySegment<ServerPlayerTick>(ticksOut.ToArray()),
        };

        return tick;
    }

    /// <summary>
    /// Applies a server's tick to the game state
    /// </summary>
    private void ApplyServerTick(ServerTickMessage tickMessage)
    {
        foreach (var tick in tickMessage.ticks)
        {
            Character character = Netplay.singleton.players[tick.id];

            if (character)
            {
                // Receive character state and rewind character to the server time
                PlayerSounds sounds = character.GetComponent<PlayerSounds>();
                Ticker ticker = character.GetComponent<Ticker>();

                sounds.ReceiveSoundHistory(tick.sounds);

                if (tick.id == Netplay.singleton.localPlayerId)
                {
                    localPlayerPing = ticker.playbackTime - tickMessage.confirmedClientTime; // which client time...?
                    ticker.Rewind(tick.moveState.state, tickMessage.confirmedClientTime);
                }
                else
                {
                    ticker.PushInput(tick.moveState.input, tickMessage.serverTime);
                    ticker.Rewind(tick.moveState.state, tickMessage.serverTime);
                }
            }
        }

        predictedServerTime = tickMessage.serverTime + localPlayerPing;
    }

    private void OnRecvServerPlayerTick(ServerTickMessage tickMessage)
    {
        if (!NetworkServer.active) // server and host should not receive server player ticks dur
            serverTickFlow.PushMessage(tickMessage, tickMessage.serverTime);
    }

    private void OnRecvClientInput(NetworkConnection source, ClientPlayerInput inputMessage)
    {
        if (source.identity && source.identity.TryGetComponent(out Player client))
        {
            if (!playerInputFlow.ContainsKey(client.playerId))
            {
                // lazy-create a player input flow for this player
                playerInputFlow.Add(client.playerId, new NetFlowController<InputPack>());
                playerInputFlow[client.playerId].flowControlSettings = clientFlowControlSettings;
            }

            float endTime = inputMessage.inputPack.startTime;
            foreach (var input in inputMessage.inputPack.inputs)
                endTime += input.deltaTime;

            playerInputFlow[client.playerId].PushMessage(inputMessage.inputPack, endTime);
        }
        else
        {
            Log.WriteWarning($"Cannot receive message from {source}: no player found");
        }
    }

    public float GetPlayerDelay(int playerId)
    {
        if (playerInputFlow.TryGetValue(playerId, out NetFlowController<InputPack> flow))
            return flow.currentDelay;
        else
            return 0f;
    }

    public string DebugInfo()
    {
        string playerInputFlowDebug = "";
        
        foreach (var pair in playerInputFlow)
        {
            if (Netplay.singleton.players[pair.Key])
            {
                playerInputFlowDebug += $"{Netplay.singleton.players[pair.Key].playerName} delay: {pair.Value}\n";
            }
        }

        return $"ServerTickFlow: {serverTickFlow}\nPing: {(int)(localPlayerPing * 1000)}ms\n{playerInputFlowDebug}";
    }
}
