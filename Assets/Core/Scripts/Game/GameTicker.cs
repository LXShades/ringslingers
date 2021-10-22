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
    public struct ServerTickMessage : NetworkMessage
    {
        public float serverTime; // time of the server
        public float lastClientEarlyness; // lateness of last input received from this client
        public ArraySegment<ServerPlayerState> ticks;
    }

    /// <summary>
    /// Per-player state from server to clients indicating a player's current state
    /// </summary>
    public struct ServerPlayerState
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

    [Header("Game")]
    [Space, Tooltip("Maximum delta time allowed between game ticks")]
    public float maxDeltaTime = 1f / 30f;

    [Header("Time")]
    public float maxTimeSmoothing = 0.2f;
    [Tooltip("WARNING: Set by GamePreferences.extraSmoothing and its defaults")]
    public float extraSmoothing = 0.0017f;
    [Tooltip("Time, in seconds, that we can go without receiving server info before lag occurs")]
    public float timeTilLag = 0.75f;

    [Header("Prediction")]
    [Range(0.1f, 1f), Tooltip("[client] How far ahead the player can predict")]
    public float maxLocalPrediction = 1f;
    [Range(0.01f, 1f), Tooltip("[client] The length of buffered inputs to send to the server for safety, in seconds. Shorter means lower net traffic but possibly more missed inputs under bad net conditions")]
    public float sendBufferLength = 0.15f;

    [Space, Tooltip("Whether to extrapolate the local character's movement")]
    public bool extrapolateLocalInput = true;

    [Header("Remote playback")]
    [Tooltip("[client] Whether to extrapolate remote clients' movement based on their last input.")]
    public bool predictRemotes = true;
    [Range(0f, 1f), Tooltip("[client] How far ahead we can predict other players' movements, maximally")]
    public float maxRemotePrediction = 0.3f;
    [Range(0.01f, 0.5f), Tooltip("[client] When predicting other players, this is the tick rate to tick them by. Should be small enough for accuracy but high enough to avoid spending too much processing time on them.")]
    public float remotePredictionTickRate = 0.08f;

    // preallocated outgoing player ticks
    private readonly List<ServerPlayerState> ticksOut = new List<ServerPlayerState>(32);

    // [client] Incoming server tick flow
    private ServerTickMessage incomingServerTick = default;
    private bool hasIncomingServerTick = false;

    public ServerTickMessage lastProcessedServerTick { get; private set; }
    public float realtimeOfLastProcessedServerTick { get; private set; }

    // ================== TIMING SECTION ================
    // [client] what server time they are predicting into
    // [server] actual server time
    // client predicted server time will be extrapolated based on ping and prediction settings
    public float predictedServerTime { get; private set; }

    // ==================== LOCAL PLAYER SECTION ===================
    // [client] returns the local player's ping based on their predicted server time
    public float localPlayerPing { get; private set; }

    // [client] When we send inputs to the server due for our predicted server time, it typically arrives slightly earlier or later than the time the server would like it (to keep inputs in the buffer)
    // At the beginning we're completely out of sync and it will usually arrive at an extremely out-of-sync time - but that's ok, the server will tell us how late (<0) or early (>0) it arrived in seconds
    // We track this with multiple samples in this history and use this to decide what our clock should be, relative to the server.
    // Time is Time.realtimeSinceStartup
    private TimelineList<float> clientInputEarlynessHistory = new TimelineList<float>();
    private TimelineList<float> serverTimeAheadnessHistory = new TimelineList<float>();

    private List<float> tempSortedList = new List<float>();

    private float clientInputEarlynessHistorySamplePeriod = 3f;

    private float currentClientServerTimeOffset = 0f;

    private float currentTimeSmoothing;

    // [client/server] returns the latest local player input this tick
    public PlayerInput localPlayerInput;

    private void OnEnable()
    {
        GamePreferences.onPreferencesChanged += OnPreferencesChanged;
    }

    private void OnDisable()
    {
        GamePreferences.onPreferencesChanged -= OnPreferencesChanged;
    }

    private void Awake()
    {
        singleton = this;

        NetworkClient.RegisterHandler<ServerTickMessage>(OnRecvServerPlayerTick);
        NetworkServer.RegisterHandler<ClientPlayerInput>(OnRecvClientInput);

        extraSmoothing = GamePreferences.extraSmoothing;
    }

    private void Update()
    {
        // Advance the clock
        // servers simply use Time.time, clients use an offset from Time.time to approximate predictedServerTime based on how late/early the server received their inputs
        if (NetworkServer.active)
            predictedServerTime = Time.time;
        else
        {
            RefreshClientServerTimeOffset();

            if (Time.realtimeSinceStartup - realtimeOfLastProcessedServerTick < timeTilLag + 1f / Netplay.singleton.playerTickrate) // add tickrate because we _should_ expect to wait that long
                predictedServerTime = Time.time + currentClientServerTimeOffset;
            else
                predictedServerTime += Time.deltaTime * 0.01f;
        }

        // Receive incoming messages
        ReceiveIncomings();

        // Camera updates go first
        if (GameManager.singleton.camera)
            GameManager.singleton.camera.UpdateAim();

        // Run our own inputs
        float quantizedTime = TimeTool.Quantize(predictedServerTime, 60);
        if (Netplay.singleton.localPlayer && quantizedTime != Netplay.singleton.localPlayer.ticker.inputTimeline.LatestTime)
        {
            // Receive local player inputs
            Vector3 localPlayerUp = Netplay.singleton.localPlayer.GetComponent<PlayerCharacterMovement>().up;

            localPlayerInput.aimDirection = Netplay.singleton.localPlayer.movement.forward.normalized; // adjust the aim to the character's aim (only so that forward adjustments are applied, this might be risky in laggy games...)
            localPlayerInput = PlayerInput.MakeLocalInput(localPlayerInput, localPlayerUp);

            // Send inputs to the local player's ticker
            Netplay.singleton.localPlayer.ticker.InsertInput(localPlayerInput, quantizedTime);
        }

        // We have all server inputs and our own inputs, tick the game
        TickGame();

        // Client/server send messages
        SendFinalOutgoings();
    }

    private float nextTimeAdjustment;

    private void RefreshClientServerTimeOffset()
    {
        const int testRate = 1;
        float timeAdjustmentSpeed = 0.07f; // in seconds per second... per second (if 0.1, it takes a second to accelerate or slow down time by 0.1)
        float maxTimeAdjustmentDuration = 0.5f; // never spend more than this long adjusting time

        if (TimeTool.IsTick(Time.unscaledTime, Time.unscaledDeltaTime, testRate))
        {
            // Update smoothing
            const float measurementPeriod = 0.75f / testRate;

            tempSortedList.Clear();

            for (int i = 0; i < clientInputEarlynessHistory.Count && clientInputEarlynessHistory.TimeAt(i) >= Time.realtimeSinceStartup - measurementPeriod; i++)
                tempSortedList.Add(clientInputEarlynessHistory[i]);
            tempSortedList.Sort();

            if (tempSortedList.Count > 0)
            {
                currentTimeSmoothing = Mathf.Min(extraSmoothing, maxTimeSmoothing);

                nextTimeAdjustment = tempSortedList[(int)(tempSortedList.Count * 0.02f)] - currentTimeSmoothing;
            }
        }

        if (nextTimeAdjustment != 0f)
        {
            if (Mathf.Abs(nextTimeAdjustment) / timeAdjustmentSpeed < maxTimeAdjustmentDuration)
            {
                float smoothed = Mathf.Clamp(nextTimeAdjustment, -timeAdjustmentSpeed * Time.deltaTime, timeAdjustmentSpeed * Time.deltaTime);
                currentClientServerTimeOffset -= smoothed;
                nextTimeAdjustment -= smoothed;
            }
            else
            {
                currentClientServerTimeOffset -= nextTimeAdjustment;
                nextTimeAdjustment = 0f;
            }

            // todo: won't this oppose the server's inputs?
            if (Netplay.singleton.localPlayer) // if time has been shifted backwards, this clears future inputs so they don't conflict with our upcoming real inputs.
                Netplay.singleton.localPlayer.ticker.inputTimeline.TrimAfter(Time.time + currentClientServerTimeOffset);
        }
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

            clientInputEarlynessHistory.Insert(Time.realtimeSinceStartup, incomingServerTick.lastClientEarlyness);
            clientInputEarlynessHistory.TrimBefore(Time.realtimeSinceStartup - clientInputEarlynessHistorySamplePeriod);
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
                // most players tick the same
                // except for remote players on clients - clients do not know these players' input history, so they should not play deltas as they will usually be inaccurate
                player.ticker.Seek(predictedServerTime, !isServer && player != Netplay.singleton.localPlayer ? TickerSeekFlags.IgnoreDeltas : 0);
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

                // we need to send it to each of them individually due to differing client times
                for (int i = 0; i < tick.ticks.Count; i++)
                {
                    Character player = Netplay.singleton.players[tick.ticks.Array[tick.ticks.Offset + i].id];

                    if (player.netIdentity.connectionToClient != null) // bots don't have a connection
                    {
                        Player client = player.connectionToClient.identity.GetComponent<Player>();

                        tick.lastClientEarlyness = player.ticker.inputTimeline.LatestTime - predictedServerTime;

                        player.netIdentity.connectionToClient.Send(tick, Channels.Unreliable);
                    }
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

                if (character != Netplay.singleton.localPlayer) // local player's inputs are more accurate timing-wise, don't interweave them with poorly estimated inputs (serverTime is NOT the exact time the inputs went in!)
                    ticker.InsertInput(tick.lastInput, tickMessage.serverTime);

                ticker.ConfirmStateAt(tick.state, TimeTool.Quantize(tickMessage.serverTime, 60));
            }
        }

        localPlayerPing = predictedServerTime - tickMessage.serverTime;
        lastProcessedServerTick = tickMessage;
        realtimeOfLastProcessedServerTick = Time.realtimeSinceStartup;

        serverTimeAheadnessHistory.Insert(Time.realtimeSinceStartup, predictedServerTime - tickMessage.serverTime);
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
            if (Netplay.singleton.players[client.playerId])
            {
                Netplay.singleton.players[client.playerId].ticker.InsertInputPack(inputMessage.inputPack);

                // This is now set during a server tick and measured based on the latest input available
                //if (inputMessage.inputPack.times.Length > 0)
                   // client.lastInputEarlyness = inputMessage.inputPack.times[0] - predictedServerTime;
            }
        }
        else
        {
            Log.WriteWarning($"Cannot receive input message from {source}: no player found");
        }
    }

    public void OnRecvBotInput(int playerId, PlayerInput input)
    {
        if (Netplay.singleton.players[playerId])
            Netplay.singleton.players[playerId].ticker.InsertInput(input, predictedServerTime);
    }

    private void OnPreferencesChanged()
    {
        extraSmoothing = GamePreferences.extraSmoothing;
    }

    public string DebugInfo()
    {
        return $"Ping: {(int)(localPlayerPing * 1000)}ms\nSmoothing: {(int)(currentTimeSmoothing * 1000f)}ms";
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