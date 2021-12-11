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
        public double serverTime; // time of the server
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
    [Tooltip("WARNING: Set by GamePreferences.extraSmoothing and its defaults")]
    public float extraSmoothing = 0.0017f;
    [Tooltip("Time, in seconds, that we can go without receiving server info before lag occurs")]
    public float timeTilLag = 0.75f;
    public int fixedInputRate = 60;
    public int fixedPhysicsRate = 30;

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
    public double realtimeOfLastProcessedServerTick { get; private set; }

    // ================== TIMING SECTION ================
    // [client] what server time they are predicting into
    // [server] actual server time
    // client predicted server time will be extrapolated based on ping and prediction settings
    public double predictedServerTime { get; private set; }

    // [client] what server time they are predicting other players into. Usually the same as predictedServerTime, but if server rewinding is enabled this may be earlier because the server will accept earlier hits
    // [server] always the same as predictedServerTime
    public double predictedReplicaServerTime { get; private set; }

    // ==================== LOCAL PLAYER SECTION ===================
    // [client] returns the local player's ping based on their predicted server time
    public float lastLocalPlayerPing { get; private set; }

    // [client] returns the local player's ping smoothed out
    public float smoothLocalPlayerPing { get; private set; }

    // [client] When we send inputs to the server due for our predicted server time, it typically arrives slightly earlier or later than the time the server would like it (to keep inputs in the buffer)
    // At the beginning we're completely out of sync and it will usually arrive at an extremely out-of-sync time - but that's ok, the server will tell us how late (<0) or early (>0) it arrived in seconds
    // We track this with multiple samples in this history and use this to decide what our clock should be, relative to the server.
    // Time is Time.realtimeSinceStartup
    private TimelineList<float> clientInputEarlynessHistory = new TimelineList<float>();
    private TimelineList<double> clientServerTimeOffsetHistory = new TimelineList<double>(); // Time.time - incomingServerTick.serverTime

    private List<double> tempSortedList = new List<double>();

    // How long the clientInputEarlynessHistory can be, ALSO APPLIES TO clientServerTimeOffsetHistory
    private float clientInputEarlynessHistorySamplePeriod = 3f;

    private double currentClientServerTimeOffset = 0f;

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
            predictedServerTime = predictedReplicaServerTime = Time.timeAsDouble;
        else
        {
            RefreshClientServerTimeOffset();

            if (Time.realtimeSinceStartupAsDouble - realtimeOfLastProcessedServerTick < timeTilLag + 1f / Netplay.singleton.playerTickrate) // add tickrate because we _should_ expect to wait that long
                predictedServerTime = Time.timeAsDouble + currentClientServerTimeOffset;
            else
                predictedServerTime += Time.deltaTime * 0.01f;

            // replica server time should reduce player jumpiness as much as possible, by reducing how far we need to predict them, which is normally by local ping.
            // predictedServerTime - smoothLocalPlayerPing is basically no jumpiness, we just need to clamp to the tolerance setting
            predictedReplicaServerTime = predictedServerTime - Mathf.Min(smoothLocalPlayerPing, ServerState.instance.serverRewindTolerance);
        }

        // Receive incoming messages
        ReceiveIncomings();

        // Camera updates go first
        if (GameManager.singleton.camera)
            GameManager.singleton.camera.UpdateAim();

        // Run our own inputs
        double quantizedTime = TimeTool.Quantize(predictedServerTime, fixedInputRate);
        if (quantizedTime != Netplay.singleton.localPlayer.ticker.inputTimeline.LatestTime)
        {
            if (Netplay.singleton.localPlayer)
            {
                // Receive local player inputs
                localPlayerInput = PlayerInput.MakeLocalInput(localPlayerInput);

                // Send inputs to the local player's ticker
                Netplay.singleton.localPlayer.ticker.InsertInput(localPlayerInput, quantizedTime);
            }

            if (isServer)
            {
                foreach (Character character in Netplay.singleton.players)
                {
                    if (character && character.serverOwningPlayer.TryGetComponent(out BotController bot))
                    {
                        bot.OnInputTick();
                    }
                }
            }
        }

        // We have all server inputs and our own inputs, tick the game
        TickGame();

        // and simulate physics
        if (TimeTool.IsTick(Time.unscaledTimeAsDouble, Time.unscaledDeltaTime, fixedPhysicsRate))
            Physics.Simulate(1f / fixedPhysicsRate);

        // Client/server send messages
        SendFinalOutgoings();
    }

    private double nextTimeAdjustment;

    private void RefreshClientServerTimeOffset()
    {
        const int testRate = 1;
        float timeAdjustmentSpeed = 0.07f; // in seconds per second... per second (if 0.1, it takes a second to accelerate or slow down time by 0.1)
        float maxTimeAdjustmentDuration = 0.5f; // never spend more than this long adjusting time

        if (TimeTool.IsTick(Time.unscaledTimeAsDouble, Time.unscaledDeltaTime, testRate))
        {
            // Update smoothing
            const float measurementPeriod = 0.75f / testRate;

            // Client time smoothing and adjustment
            tempSortedList.Clear();
            for (int i = 0; i < clientInputEarlynessHistory.Count && clientInputEarlynessHistory.TimeAt(i) >= Time.realtimeSinceStartupAsDouble - measurementPeriod; i++)
                tempSortedList.Add(clientInputEarlynessHistory[i]);
            tempSortedList.Sort();

            if (tempSortedList.Count > 0)
            {
                currentTimeSmoothing = extraSmoothing;

                nextTimeAdjustment = tempSortedList[(int)(tempSortedList.Count * 0.02f)] - currentTimeSmoothing;
            }

            // Server ping averaging
            tempSortedList.Clear();
            for (int i = 0; i < clientServerTimeOffsetHistory.Count && clientServerTimeOffsetHistory.TimeAt(i) >= Time.realtimeSinceStartupAsDouble - measurementPeriod; i++)
                tempSortedList.Add(clientServerTimeOffsetHistory[i]);
            tempSortedList.Sort();

            if (tempSortedList.Count > 0)
            {
                double targetServerPredictedTime = Time.timeAsDouble + currentClientServerTimeOffset - nextTimeAdjustment;
                double targetServerTimeMedian = Time.timeAsDouble + tempSortedList[tempSortedList.Count / 2];

                smoothLocalPlayerPing = (float)(targetServerPredictedTime - targetServerTimeMedian);
            }
        }

        if (nextTimeAdjustment != 0d)
        {
            if (Math.Abs(nextTimeAdjustment) / timeAdjustmentSpeed < maxTimeAdjustmentDuration)
            {
                float smoothed = Mathf.Clamp((float)nextTimeAdjustment, -timeAdjustmentSpeed * Time.deltaTime, timeAdjustmentSpeed * Time.deltaTime);
                currentClientServerTimeOffset -= smoothed;
                nextTimeAdjustment -= smoothed;
            }
            else
            {
                currentClientServerTimeOffset -= nextTimeAdjustment;
                nextTimeAdjustment = 0d;
            }

            // todo: won't this oppose the server's inputs?
            if (Netplay.singleton.localPlayer) // if time has been shifted backwards, this clears future inputs so they don't conflict with our upcoming real inputs.
                Netplay.singleton.localPlayer.ticker.inputTimeline.TrimAfter(Time.timeAsDouble + currentClientServerTimeOffset);
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

            clientInputEarlynessHistory.Insert(Time.realtimeSinceStartupAsDouble, incomingServerTick.lastClientEarlyness);
            clientInputEarlynessHistory.TrimBefore(Time.realtimeSinceStartupAsDouble - clientInputEarlynessHistorySamplePeriod);

            clientServerTimeOffsetHistory.Insert(Time.realtimeSinceStartupAsDouble, incomingServerTick.serverTime - Time.timeAsDouble);
            clientServerTimeOffsetHistory.TrimBefore(Time.realtimeSinceStartupAsDouble - clientInputEarlynessHistorySamplePeriod);
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
                // they also may have a time offset if we're using the fancy experimental Rewind stuff
                bool isALocalPlayer = player == Netplay.singleton.localPlayer || (isServer && player.connectionToClient == null);

                player.ticker.Seek(!isServer && !isALocalPlayer ? predictedReplicaServerTime : predictedServerTime, 
                    !isServer && !isALocalPlayer ? TickerSeekFlags.IgnoreDeltas : 0);
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
                    Character character = Netplay.singleton.players[tick.ticks.Array[tick.ticks.Offset + i].id];

                    if (character.netIdentity.connectionToClient != null && character.netIdentity.connectionToClient.identity != null) // bots don't have a connection
                    {
                        tick.lastClientEarlyness = (float)(character.serverOwningPlayer.serverTimeOfLastReceivedInput - predictedServerTime);

                        character.netIdentity.connectionToClient.Send(tick, Channels.Unreliable);
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

        lastLocalPlayerPing = (float)(predictedServerTime - tickMessage.serverTime);
        lastProcessedServerTick = tickMessage;
        realtimeOfLastProcessedServerTick = Time.realtimeSinceStartupAsDouble;
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

                // Trim the history regularly
                // If we receive an old input from the future (i.e. a message sent on the previous level, or before the timer was reset)
                // then this will screw up the aheadness history, and the input history in general. Keep it trimmed
                Netplay.singleton.players[client.playerId].ticker.inputTimeline.Trim(predictedServerTime - 2f, predictedServerTime + 2f);

                if (inputMessage.inputPack.times.Length > 0)
                   client.serverTimeOfLastReceivedInput = inputMessage.inputPack.times[0];
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
        return $"Ping: {(int)(lastLocalPlayerPing * 1000)}ms\nSmoothPing: {(int)(smoothLocalPlayerPing * 1000)}ms\nSmoothingDelay: {(int)(currentTimeSmoothing * 1000f)}ms";
    }
}

public static class ClientPlayerInputReaderWriter
{
    public static void WritePlayerInputMessage(this NetworkWriter writer, GameTicker.ClientPlayerInput playerInput)
    {
        writer.WriteArray<PlayerInput>(playerInput.inputPack.inputs);
        writer.WriteArray<double>(playerInput.inputPack.times);
    }

    public static GameTicker.ClientPlayerInput ReadPlayerInputMessage(this NetworkReader reader)
    {
        GameTicker.ClientPlayerInput playerInput;

        playerInput.inputPack.inputs = reader.ReadArray<PlayerInput>();
        playerInput.inputPack.times = reader.ReadArray<double>();

        return playerInput;
    }
}