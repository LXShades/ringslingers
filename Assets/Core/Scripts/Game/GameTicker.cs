﻿using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        public ArraySegment<ServerPlayerState> playerStates;
    }

    /// <summary>
    /// Per-player state from server to clients indicating a player's current state
    /// </summary>
    public struct ServerPlayerState
    {
        public byte id;
        public CharacterState state;
        public CharacterInput lastInput;
        public byte sounds;
    }

    /// <summary>
    /// A player input message send from clients to server
    /// </summary>
    public struct ClientPlayerInput : NetworkMessage
    {
        public TickerInputPack<CharacterInput> inputPack;
    }

    [Header("Game")]
    [FormerlySerializedAs("tickerSettings")]
    public TimelineSettings timelineSettings = TimelineSettings.Default;

    [Header("Time")]
    [Tooltip("WARNING: Set by GamePreferences.extraSmoothing and its defaults")]
    public float extraSmoothing = 0.0017f;
    [Tooltip("Time, in seconds, that we can go without receiving server info before lag occurs")]
    public float timeTilLag = 0.75f;
    public int fixedInputRate => timelineSettings.fixedTickRate;
    public int fixedPhysicsRate = 30;

    [Header("Prediction")]
    [Range(0.1f, 1f), Tooltip("[client] How far ahead the player can predict")]
    public float maxLocalPrediction = 1f;
    [Range(0.01f, 1f), Tooltip("[client] The length of buffered inputs to send to the server for safety, in seconds. Shorter means lower net traffic but possibly more missed inputs under bad net conditions")]
    public float sendBufferLength = 0.15f;

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

    /// <summary>
    /// This is the timeline for the game! This is where the ticks and rewinds happen
    /// </summary>
    private Timeline timeline = new Timeline("GameTimeline");

    // [client] When we send inputs to the server due for our predicted server time, it typically arrives slightly earlier or later than the time the server would like it (to keep inputs in the buffer)
    // At the beginning we're completely out of sync and it will usually arrive at an extremely out-of-sync time - but that's ok, the server will tell us how late (<0) or early (>0) it arrived in seconds
    // We track this with multiple samples in this history and use this to decide what our clock should be, relative to the server.
    // Time is Time.realtimeSinceStartup
    private TimelineTrack<float> clientInputEarlynessHistory = new TimelineTrack<float>();
    private TimelineTrack<double> clientServerTimeOffsetHistory = new TimelineTrack<double>(); // Time.time - incomingServerTick.serverTime

    private List<double> tempSortedList = new List<double>();

    // How long the clientInputEarlynessHistory can be, ALSO APPLIES TO clientServerTimeOffsetHistory
    private float clientInputEarlynessHistorySamplePeriod = 3f;

    private double currentClientServerTimeOffset = 0f;

    private float currentTimeSmoothing;

    // [client/server] returns the latest local player input this tick
    public CharacterInput localPlayerInput;

    private void OnEnable()
    {
        GamePreferences.onPreferencesChanged += OnPreferencesChanged;

        WhenReady<GameTicker>.Register(this);
    }

    private void OnDisable()
    {
        GamePreferences.onPreferencesChanged -= OnPreferencesChanged;

        WhenReady<GameTicker>.Unregister(this);
    }

    private void Awake()
    {
        singleton = this;

        NetworkClient.RegisterHandler<ServerTickMessage>(OnRecvServerPlayerTick);
        NetworkServer.RegisterHandler<ClientPlayerInput>(OnRecvClientInput);

        extraSmoothing = GamePreferences.inputSmoothing;
    }

    private void Update()
    {
        GameState_ServerSettings.Settings serverSettings = GameState.Get(out GameState_ServerSettings serverSettingsComponent) ? serverSettingsComponent.settings : GameState_ServerSettings.Settings.Default;

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
                predictedServerTime += Time.deltaTime * 0.01f; // lag

            // replica server time should reduce player jumpiness as much as possible, by reducing how far we need to predict them, which is normally by local ping.
            // predictedServerTime - smoothLocalPlayerPing is basically no jumpiness, we just need to clamp to the tolerance setting
            predictedReplicaServerTime = predictedServerTime - Mathf.Min(smoothLocalPlayerPing, serverSettings.hitLagCompensation);
        }

        // Receive incoming messages
        ReceiveIncomings();

        // Camera updates go first
        if (GameManager.singleton.camera)
            GameManager.singleton.camera.UpdateAim();

        // Run our own inputs
        double quantizedTime = TimeTool.Quantize(predictedServerTime, fixedInputRate);
        Character localPlayer = Netplay.singleton.localPlayer;

        if ((NetworkServer.active && quantizedTime > TimeTool.Quantize(predictedServerTime - Time.deltaTime, fixedInputRate))
            || (localPlayer && quantizedTime > localPlayer.entity.inputTrack.LatestTime))
        {
            if (localPlayer)
            {
                // Receive local player inputs
                localPlayerInput = CharacterInput.MakeLocalInput(localPlayerInput);

                // Send inputs to the local player's ticker
                localPlayer.entity.InsertInput(localPlayerInput, quantizedTime);
            }

            if (isServer)
            {
                foreach (Character character in Netplay.singleton.players)
                {
                    if (character && character.serverOwningPlayer.TryGetComponent(out BotController bot))
                        bot.OnInputTick();
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
    public Timeline.Entity<TState, TInput> RegisterEntity<TState, TInput>(NetworkBehaviour netBehaviour, ITickable<TState, TInput> tickable) where TState : ITickerState<TState> where TInput : ITickerInput<TInput>
    {
        return timeline.AddEntity(netBehaviour.gameObject.name, tickable, (int)netBehaviour.netId);
    }

    public void UnregisterEntity(ITickableBase tickable)
    {
        timeline.RemoveEntity(tickable);
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
                Netplay.singleton.localPlayer.entity.inputTrack.TrimAfter(Time.timeAsDouble + currentClientServerTimeOffset);
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
            foreach (Character character in Netplay.singleton.players)
            {
                if (character && character.TryGetComponent(out TimelineEntityInterpolator interpolator))
                    interpolator.OnAboutToRecalculateLatestState();
            }

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
        // Most players tick the same
        // Except for remote players on clients - clients do not accurately know these players' input history, so they should not play deltas as they will usually be incorrect
        // -> This is most noticeable when a player jumps and appears to thok and correct itself. We received the jump input after the moment of jump but not at the moment, and the delta gets applied too late, resulting in the double-jump.
        // They also may have a time offset if we're using the fancy experimental Rewind stuff
        if (!NetworkServer.active)
        {
            foreach (Character player in Netplay.singleton.players)
            {
                if (player)
                {
                    if (player != Netplay.singleton.localPlayer)
                        player.entity.seekFlags = EntitySeekFlags.NoInputDeltas;
                    else
                        player.entity.seekFlags = EntitySeekFlags.None;
                }
            }
        }

        // Tick all players
        timeline.settings = timelineSettings;
        timeline.Seek(predictedServerTime);
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
                for (int i = 0; i < tick.playerStates.Count; i++)
                {
                    Character character = Netplay.singleton.players[tick.playerStates.Array[tick.playerStates.Offset + i].id];

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
                Timeline.Entity<CharacterState, CharacterInput> localTicker = Netplay.singleton.localPlayer != null ? Netplay.singleton.localPlayer.entity : null;

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
                int stateForTime = character.entity.stateTrack.ClosestIndexBeforeInclusive(predictedServerTime);
                int inputForTime = character.entity.inputTrack.ClosestIndexBeforeOrEarliest(predictedServerTime); // We might have received later inputs so make sure we send the right one that accompanies the state

                ticksOut.Add(new ServerPlayerState()
                {
                    id = (byte)i,
                    sounds = sounds.soundHistory,
                    state = stateForTime != -1 ? character.entity.stateTrack[character.entity.stateTrack.ClosestIndexBeforeInclusive(predictedServerTime)] : character.entity.latestState,
                    lastInput = inputForTime != -1 ? character.entity.inputTrack[inputForTime] : default
                });
            }
        }

        ServerTickMessage tick = new ServerTickMessage()
        {
            serverTime = predictedServerTime,
            playerStates = new ArraySegment<ServerPlayerState>(ticksOut.ToArray()),
        };

        return tick;
    }

    /// <summary>
    /// Applies a server's tick to the game state
    /// </summary>
    private void ApplyServerTick(ServerTickMessage tickMessage)
    {
        double gameStateTime = TimeTool.Quantize(tickMessage.serverTime, fixedInputRate);
        double replicaStateTime = TimeTool.Quantize(tickMessage.serverTime + (predictedServerTime - predictedReplicaServerTime), fixedInputRate); // replicas might not tick as far ahead

        foreach (ServerPlayerState playerState in tickMessage.playerStates)
        {
            Character character = Netplay.singleton.players[playerState.id];

            if (character)
            {
                // Receive character state and rewind character to the server time
                // We do a hack with replicas if server lag compensation is on. We don't want to predict them as far, but as of writing Timeline only ticks everything to the same time
                // So as a trick to predict them less, we shift their states further ahead on the timeline
                double effectiveStateTime = character == Netplay.singleton.localPlayer ? gameStateTime : replicaStateTime;
                PlayerSounds sounds = character.GetComponent<PlayerSounds>();
                Timeline.Entity<CharacterState, CharacterInput> entity = character.entity;

                sounds.ReceiveSoundHistory(playerState.sounds);

                if (character != Netplay.singleton.localPlayer) // local player's inputs are more accurate timing-wise so don't overwrite them
                    entity.InsertInput(playerState.lastInput, effectiveStateTime);

                entity.StoreStateAt(playerState.state, effectiveStateTime);
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
                Netplay.singleton.players[client.playerId].entity.InsertInputPack(inputMessage.inputPack);

                // Trim the history regularly
                // If we receive an old input from the future (i.e. a message sent on the previous level, or before the timer was reset)
                // then this will screw up the aheadness history, and the input history in general. Keep it trimmed
                Netplay.singleton.players[client.playerId].entity.inputTrack.Trim(predictedServerTime - 2f, predictedServerTime + 2f);

                if (inputMessage.inputPack.times.Length > 0)
                   client.serverTimeOfLastReceivedInput = inputMessage.inputPack.times[0];
            }
        }
        else
        {
            Log.WriteWarning($"Cannot receive input message from {source}: no player found");
        }
    }

    public void OnRecvBotInput(int playerId, CharacterInput input)
    {
        if (Netplay.singleton.players[playerId])
            Netplay.singleton.players[playerId].entity.InsertInput(input, predictedServerTime);
    }

    private void OnPreferencesChanged()
    {
        extraSmoothing = GamePreferences.inputSmoothing;
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
        writer.WriteArray<CharacterInput>(playerInput.inputPack.inputs);
        writer.WriteArray<double>(playerInput.inputPack.times);
    }

    public static GameTicker.ClientPlayerInput ReadPlayerInputMessage(this NetworkReader reader)
    {
        GameTicker.ClientPlayerInput playerInput;

        playerInput.inputPack.inputs = reader.ReadArray<CharacterInput>();
        playerInput.inputPack.times = reader.ReadArray<double>();

        return playerInput;
    }
}