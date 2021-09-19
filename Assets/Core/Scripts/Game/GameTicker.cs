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
        const float kMaxExtrapolatedTimeOffset = 0.5f;

        public float serverTime; // time of the server
        public float confirmedClientTime; // time of the receiving client, as last seen on the server
        public float extrapolatedClientTime => confirmedClientTime + clientTimeExtrapolation; // time of the receiving client, as last seen on the server, according to the server's smooth extrapolated playback of the client for more smoothness
        public float clientTimeExtrapolation
        {
            set => _compressedExtrapolatedClientTime = (byte)Mathf.Min((int)(value * 256f / kMaxExtrapolatedTimeOffset), 255);
            get => _compressedExtrapolatedClientTime * kMaxExtrapolatedTimeOffset / 256f;
        }
        public byte _compressedExtrapolatedClientTime;
        public ArraySegment<ServerPlayerState> ticks;
    }

    /// <summary>
    /// Per-player state from server to clients indicating a player's current state
    /// </summary>
    private struct ServerPlayerState
    {
        public byte id;
        public CharacterState state;
        public PlayerInput lastInput;
        public byte sounds;
    }

    /// <summary>
    /// A player input message send from clients to server
    /// </summary>
    public struct ClientPlayerInput : NetworkMessage
    {
        public TickerInputPack<PlayerInput> inputPack;
    }

    // Client/server flow control settings for better network smoothing
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

    // preallocated outgoing player ticks
    private readonly List<ServerPlayerState> ticksOut = new List<ServerPlayerState>(32);

    // [server] Incoming input flow controller per player
    private readonly Dictionary<int, List<TickerInputPack<PlayerInput>>> incomingPlayerInputs = new Dictionary<int, List<TickerInputPack<PlayerInput>>>();
    // [client] Incoming server tick flow
    private ServerTickMessage incomingServerTick = default;
    private bool hasIncomingServerTick = false;

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
    }

    private void Update()
    {
        // Advance the clock
        if (NetworkServer.active)
            predictedServerTime = Time.unscaledTime; // we technically don't run a prediction of the server time on the server
        else
            predictedServerTime += Time.unscaledDeltaTime; // we just tick it up and update properly occasionally

        // Receive incoming messages
        ReceiveIncomings();

        // Camera updates go first
        if (GameManager.singleton.camera)
            GameManager.singleton.camera.UpdateAim();

        // Add states, etc to our local prediction history
        if (Netplay.singleton.localPlayer)
        {
            // Receive local player inputs
            Vector3 localPlayerUp = Netplay.singleton.localPlayer.GetComponent<PlayerCharacterMovement>().up;

            localPlayerInput.aimDirection = Netplay.singleton.localPlayer.movement.forward.normalized; // adjust the aim to the character's aim (only so that forward adjustments are applied, this might be risky in laggy games...)
            localPlayerInput = PlayerInput.MakeLocalInput(localPlayerInput, localPlayerUp);

            // Send inputs to the local player's ticker
            Netplay.singleton.localPlayer.ticker.InsertInput(localPlayerInput, Time.time);
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
        if (hasIncomingServerTick)
        {
            ApplyServerTick(incomingServerTick);
            hasIncomingServerTick = false;
        }

        // Server receive inputs from players
        for (int i = 0; i < Netplay.singleton.players.Count; i++)
        {
            Character character = Netplay.singleton.players[i];

            if (character && incomingPlayerInputs.ContainsKey(i))
            {
                foreach (TickerInputPack<PlayerInput> inputPack in incomingPlayerInputs[i])
                {
                    character.ticker.InsertInputPack(inputPack);
                }

                character.timeOfLastInputPush = Time.time;
                incomingPlayerInputs[i].Clear();
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
                if (player == Netplay.singleton.localPlayer) // local player
                {
                    if (isServer)
                    {
                        // on server it's pretty easy, just set realtimePlaybackTime to confirmedPlaybackTime, much like the other players
                        player.ticker.Seek(Time.time, player.ticker.confirmedStateTime);
                    }
                    else
                    {
                        // on the client, it's a bit more awkward, their confirmedPlaybackTime will be rewound periodically causing jump sounds to be replayed
                        // this approach anticipates when a new, full tick is about to execute by using Time.deltaTime
                        if (Time.time - Time.deltaTime < player.ticker.inputTimeline.LatestTime && player.ticker.inputTimeline.Count > 1)
                            player.ticker.Seek(Time.time, player.ticker.inputTimeline.TimeAt(1));
                        else
                            player.ticker.Seek(Time.time, player.ticker.inputTimeline.LatestTime);
                    }
                }
                else if (isServer) // other player on server
                    player.ticker.Seek(player.ticker.inputTimeline.LatestTime + Time.time - player.timeOfLastInputPush, player.ticker.confirmedStateTime); // extrapolate the player further than the last input we got from them
                else if (isClient) // replica on client
                    player.ticker.Seek(predictedServerTime, player.ticker.playbackTime, TickerSeekFlags.IgnoreDeltas); // deltas are ignored for clients' replicas because clients don't have full input info, so they can't discern deltas (or the future)
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

                // we need to send it to each of them individually due to differing client times (

                for (int i = 0; i < tick.ticks.Count; i++)
                {
                    Character player = Netplay.singleton.players[tick.ticks.Array[tick.ticks.Offset + i].id];
                    tick.clientTimeExtrapolation = player.ticker.playbackTime - player.ticker.confirmedStateTime;
                    tick.confirmedClientTime = player.ticker.confirmedStateTime;
                    player.netIdentity.connectionToClient.Send(tick, Channels.Unreliable);
                }
            }
            else if (NetworkClient.isConnected)
            {
                // Send local player inputs to server
                Ticker<PlayerInput, CharacterState> localTicker = Netplay.singleton.localPlayer != null ? Netplay.singleton.localPlayer.ticker : null;

                if (localTicker != null)
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

                ticksOut.Add(new ServerPlayerState()
                {
                    id = (byte)i,
                    sounds = sounds.soundHistory,
                    state = character.ticker.lastConfirmedState,
                    lastInput = character.ticker.inputTimeline.Latest
                });
            }
        }

        ServerTickMessage tick = new ServerTickMessage()
        {
            serverTime = predictedServerTime,
            ticks = new ArraySegment<ServerPlayerState>(ticksOut.ToArray()),
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
                Ticker<PlayerInput, CharacterState> ticker = character.ticker;

                sounds.ReceiveSoundHistory(tick.sounds);

                if (tick.id == Netplay.singleton.localPlayerId)
                {
                    // extrapolatedClientTime is smoother as it considers how long it's been sinc ethe client did the thing...
                    // but confirmedClientTime is more accurate to where the client actually is on the server, and more accurate to the client's local time...right?
                    // after testing, extrapolatedClientTime turned out to be smoother
                    localPlayerPing = ticker.playbackTime - tickMessage.extrapolatedClientTime;
                    ticker.ConfirmStateAt(tick.state, tickMessage.confirmedClientTime); // this line definitely uses confirmedClientTime! not sure about the other!
                }
                else
                {
                    ticker.InsertInput(tick.lastInput, tickMessage.serverTime);
                    ticker.ConfirmStateAt(tick.state, tickMessage.serverTime);
                }
            }
        }

        predictedServerTime = tickMessage.serverTime + localPlayerPing;
    }

    private void OnRecvServerPlayerTick(ServerTickMessage tickMessage)
    {
        if (!NetworkServer.active) // server and host should not receive server player ticks dur
        {
            hasIncomingServerTick = true;
            incomingServerTick = tickMessage;
        }
    }

    private void OnRecvClientInput(NetworkConnection source, ClientPlayerInput inputMessage)
    {
        if (source.identity && source.identity.TryGetComponent(out Player client))
        {
            if (!incomingPlayerInputs.ContainsKey(client.playerId))
            {
                // lazy-create a player input flow for this player
                incomingPlayerInputs.Add(client.playerId, new List<TickerInputPack<PlayerInput>>());
            }

            if (inputMessage.inputPack.inputs.Length > 0)
            {
                incomingPlayerInputs[client.playerId].Add(inputMessage.inputPack);
            }
            else
            {
                Log.WriteError($"Input message from {source} has no inputs! This should never really happen!");
            }
        }
        else
        {
            Log.WriteWarning($"Cannot receive input message from {source}: no player found");
        }
    }

    public string DebugInfo()
    {
        string playerInputFlowDebug = "";
        
        return $"Ping: {(int)(localPlayerPing * 1000)}ms\n{playerInputFlowDebug}";
    }
}

public static class ClientPlayerInputReaderWriter
{
    public static void WritePlayerInputMessage(this NetworkWriter writer, GameTicker.ClientPlayerInput playerInput)
    {
        writer.WriteArray<PlayerInput>(playerInput.inputPack.inputs);
        writer.WriteArray<float>(playerInput.inputPack.times);
    }

    public static GameTicker.ClientPlayerInput ReadPlayerInputMessage(this NetworkReader reader)
    {
        GameTicker.ClientPlayerInput playerInput;

        playerInput.inputPack.inputs = reader.ReadArray<PlayerInput>();
        playerInput.inputPack.times = reader.ReadArray<float>();

        return playerInput;
    }
}