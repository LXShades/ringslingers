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
        public float clientTime; // time of the receiving client, as last seen on the server
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

    /// <summary>
    /// 
    /// </summary>
    private struct PredictionHistoryItem
    {

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

    [Space, Tooltip("Limits the player's input rate in hz, preventing flooding towards the server at high local tick rates. Recommended for now.")]
    public bool limitInputRate = true;
    [Tooltip("This should probably always be true (todo: remove?)")]
    public bool limitUpdatesToInputRate = true;
    [Tooltip("The maximum input rate allowed per player. Higher input rates mean more responsiveness but higher processing time and higher client->server net traffic.")]
    public int maxInputRate = 240;

    [Space, Tooltip("Whether to extrapolate the local character's movement")]
    public bool extrapolateLocalInput = true;

    [Space, Tooltip("Maximum delta time allowed between player ticks")]
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

    // [client] what time we're playing back
    public float clientPlaybackTime { get; private set; }
    // [client] uh it's client time
    public float clientTime { get; private set; }
    // [client] the client time, thing, uh we're extrapolating
    public float currentExtrapolation { get; private set; }

    // ==================== LOCAL PLAYER SECTION ===================
    // [client] returns the local player's ping
    public float localPlayerPing { get; private set; }

    // [client/server] returns the latest local player input this tick
    public PlayerInput localPlayerInput { get; private set; }

    // [client/server] returns the previous local player input
    public PlayerInput lastLocalPlayerInput { get; private set; }

    private HistoryList<PredictionHistoryItem> predictionHistory;

    public HistoryList<InputDelta> inputHistory = new HistoryList<InputDelta>();

    private HistoryList<MovementEvent> eventHistory = new HistoryList<MovementEvent>();

    private HistoryList<CharacterState> stateHistory = new HistoryList<CharacterState>();

    private MovementEvent pendingEvents = null;

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

        // Receive local player inputs
        Vector3 localPlayerUp = Vector3.up;

        if (Netplay.singleton.localPlayer)
            localPlayerUp = Netplay.singleton.localPlayer.GetComponent<CharacterMovement>().up;

        lastLocalPlayerInput = localPlayerInput;
        localPlayerInput = PlayerInput.MakeLocalInput(lastLocalPlayerInput, localPlayerUp);

        // Add states, etc to our local prediction history
        UpdatePredictionHistory();

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
            if (Netplay.singleton.players[i] && playerInputFlow.ContainsKey(i))
            {
                while (playerInputFlow[i].TryPopMessage(out InputPack inputPack, false)) // skipOutdatedMessages is false because we'd like to receive everything we got since the last one
                {

                    Netplay.singleton.players[i].GetComponent<PlayerController>().ReceiveInputPack(inputPack);
                }
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
                player.GetComponent<PlayerController>().Tick();
        }

        // CLIENT ONLY....
        // Playback our latest movements
        int index = inputHistory.ClosestIndexAfter(lastPlaybackInput, 0f);
        bool doExtrapolate = hasAuthority && extrapolateLocalInput;
        bool doRemoteExtrapolate = !hasAuthority && extrapolateRemoteInput;

        try
        {
            if (index != -1)
            {
                if (doExtrapolate || doRemoteExtrapolate)
                    ApplyMoveState(lastConfirmedState); // when extrapolating our own inputs, we need to restore our positions for normal playback when the time comes

                // add any pending events
                if (pendingEvents != null && pendingEvents.GetInvocationList().Length > 0)
                {
                    eventHistory.Insert(inputHistory.TimeAt(index), pendingEvents);
                    pendingEvents = null;
                }

                // execute the ticks
                for (int i = index; i >= 0; i--)
                {
                    float time = inputHistory.TimeAt(i);
                    var events = eventHistory.ItemAt(time);

                    if (events != null)
                        events?.Invoke(movement, false);

                    movement.TickMovement(inputHistory[i].deltaTime, PlayerInput.MakeWithDeltas(inputHistory[i].input, inputHistory.Count > i + 1 ? inputHistory[i + 1].input : inputHistory[i].input), false);
                    lastPlaybackInput = time;
                }

                clientPlaybackTime = inputHistory.LatestTime + inputHistory.Latest.deltaTime; // precision correction
                currentExtrapolation = 0f;
                lastConfirmedState = MakeMoveState();

                if (debugSelfReconcile && stateHistory.ClosestIndexBefore(lastPlaybackInput - debugSelfReconcileDelay) != -1)
                {
                    // preserve state so we don't bake in incorrect movements
                    CharacterState backup = lastConfirmedState;
                    int anotherIndex = stateHistory.ClosestIndexBefore(lastPlaybackInput - debugSelfReconcileDelay);
                    TryReconcile(stateHistory[anotherIndex]);
                    lastConfirmedState = backup;
                    ApplyMoveState(lastConfirmedState);
                }
            }
            else if (doExtrapolate)
            {
                movement.TickMovement(Time.deltaTime, PlayerInput.MakeWithDeltas(player.input, player.input), true);
                currentExtrapolation += Time.deltaTime;
            }
            else if (doRemoteExtrapolate)
            {
                movement.TickMovement(Time.deltaTime, inputHistory.Latest.input, true);
                currentExtrapolation += Time.deltaTime;
            }
        }
        catch (Exception e)
        {
            Log.WriteException(e);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void UpdatePredictionHistory()
    {
        clientTime += Time.deltaTime;

        if (hasAuthority && (!limitInputRate || clientTime >= nextInputUpdateTime))
        {
            // Add current player input to input history
            inputHistory.Insert(Mathf.Max(clientPlaybackTime, clientTime - maxDeltaTime), new InputDelta(localPlayerInput, Mathf.Min(clientTime - clientPlaybackTime, maxDeltaTime)));
            nextInputUpdateTime = clientTime + 1f / maxInputRate;

            if (Netplay.singleton.localPlayer)
                stateHistory.Insert(Mathf.Max(clientPlaybackTime, clientTime - maxDeltaTime), Netplay.singleton.localPlayer.MakeState());
        }

        float inputHistoryMaxLength = pingTolerance + Mathf.Min(1f / (Mathf.Min(Netplay.singleton.playerTickrate, limitInputRate ? maxInputRate : 1000f)), 5f);
        float trimTo = Mathf.Max(clientTime, clientPlaybackTime) - inputHistoryMaxLength;

        inputHistory.Prune(trimTo);
        eventHistory.Prune(trimTo);
        stateHistory.Prune(trimTo);
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
                ServerTickMessage tick;
                
                tick = MakeTickMessage();

                for (int i = 0; i < tick.ticks.Count; i++)
                {
                    Character player = Netplay.singleton.players[tick.ticks.Array[tick.ticks.Offset + i].id];
                    tick.clientTime = player.clientTime;
                    NetworkServer.SendToClientOfPlayer(player.netIdentity, tick, Channels.DefaultUnreliable);
                }
                NetworkServer.SendToAll(tick, Channels.DefaultUnreliable, true);
            }
            else if (NetworkClient.isConnected)
            {
                if (Netplay.singleton.localPlayer)
                {
                    InputPack inputPack = Netplay.singleton.localPlayer.GetComponent<PlayerController>().MakeInputPack();

                    NetworkClient.Send(new ClientPlayerInput() { inputPack = inputPack }, Channels.DefaultUnreliable);
                }
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
            if (Netplay.singleton.players[i])
            {
                PlayerController controller = Netplay.singleton.players[i].GetComponent<PlayerController>();
                PlayerSounds sounds = controller.GetComponent<PlayerSounds>();

                ticksOut.Add(new ServerPlayerTick()
                {
                    id = (byte)i,
                    sounds = sounds.soundHistory,
                    moveState = new MoveStateWithInput()
                    {
                        state = controller.MakeOrGetConfirmedMoveState(),
                        input = inputHistory.Latest.input
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
        float localPlayerTimeOnServer = -1f;

        foreach (var tick in tickMessage.ticks)
        {
            if (Netplay.singleton.players[tick.id])
            {
                PlayerController controller = Netplay.singleton.players[tick.id].GetComponent<PlayerController>();
                PlayerSounds sounds = controller.GetComponent<PlayerSounds>();
                controller.ReceiveMovement(tick.moveState.state, tick.moveState.input);
                sounds.ReceiveSoundHistory(tick.sounds);

                if (tick.id == Netplay.singleton.localPlayerId)
                {
                    localPlayerTimeOnServer = tick.moveState.
                    localPlayerPing = controller.clientPlaybackTime + controller.currentExtrapolation - (tick.moveState.state.time + tick.moveState.state.extrapolation);
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

    [Header("Debug")]
#if UNITY_EDITOR
    public float debugSelfReconcileDelay = 0.3f;
    public bool debugSelfReconcile = false;

    public bool debugDrawReconciles = false;
#else
    // just don't do these debug things in builds
    [NonSerialized]
    public float debugSelfReconcileDelay = 0.3f;
    [NonSerialized]
    public bool debugSelfReconcile = false;
    [NonSerialized]
    public bool debugDrawReconciles = false;
#endif


    private bool hasReceivedMoveState = false;
    private CharacterState lastReceivedMoveState;
    private PlayerInput lastReceivedMoveStateInput; // valid on client when receiving a player from the server

    private float lastPlaybackInput = -1f;

    CharacterState lastConfirmedState;

    private float nextInputUpdateTime;

    private void ProcessReceivedStates()
    {
        if (hasReceivedMoveState && !NetworkServer.active)
        {
            // validate this player (or our own) movement on the client
            ClientValidateMovement(lastReceivedMoveState, lastReceivedMoveStateInput);
        }

        hasReceivedMoveState = false;
    }

    public void ReceiveInputPack(InputPack inputPack)
    {
        float time = inputPack.startTime;

        for (int i = inputPack.inputs.Length - 1; i >= 0; i--)
        {
            inputHistory.Set(time, inputPack.inputs[i], 0.001f);
            time += inputPack.inputs[i].deltaTime;
        }

        ReceiveMovement(inputPack.state);

        clientTime = time;
        player.input = inputHistory.Latest.input;
    }

    private void ClientValidateMovement(CharacterState moveState, PlayerInput input)
    {
        if (!hasAuthority)
        {
            // this is another player, just plop them here
            ApplyMoveState(moveState);

            player.input = input;
            clientTime = moveState.time;
            lastPlaybackInput = moveState.time;

            inputHistory.Insert(moveState.time, new InputDelta(input, 0f)); // todo

            // Run latest prediction
            float predictionAmount = Mathf.Min(maxRemotePrediction, GameTicker.singleton ? GameTicker.singleton.localPlayerPing : 0f);

            if (extrapolateRemoteInput)
                predictionAmount -= Time.deltaTime; // we're gonna replay this again anyway

            // try and get events working... pop any new predictions into the pendingEvents
            if (pendingEvents != null && pendingEvents.GetInvocationList().Length > 0)
            {
                eventHistory.Insert(moveState.time + predictionAmount - 0.001f, pendingEvents);
                pendingEvents = null;
            }

            for (float t = 0; t < predictionAmount; t += remotePredictionTimeStep)
            {
                float delta = Mathf.Min(remotePredictionTimeStep, predictionAmount - t);
                int closestFollowingEvent = eventHistory.ClosestIndexAfter(moveState.time + t);

                if (closestFollowingEvent != -1 && eventHistory.TimeAt(closestFollowingEvent) <= moveState.time + t + delta)
                    eventHistory[closestFollowingEvent]?.Invoke(movement, true);

                movement.TickMovement(delta, input, true);
            }

            currentExtrapolation = predictionAmount;
        }
        else
        {
            TryReconcile(moveState);
        }
    }

    private void TryReconcile(CharacterState pastState)
    {
        Vector3 position = transform.position;
        Debug.Assert((hasAuthority && !NetworkServer.active) || debugSelfReconcile);

        // hop back to server position
        ApplyMoveState(pastState);

        // fast forward if we can
        int startState = inputHistory.ClosestIndexBefore(pastState.time);

        if (startState != -1)
        {
            if (debugDrawReconciles)
            {
                MakeMoveState().DebugDraw(Color.blue);
                stateHistory[startState].DebugDraw(Color.red);
            }

            // resimulate to our latest position
            for (int i = startState; i >= 0; i--)
            {
                PlayerInput inputWithDeltas = PlayerInput.MakeWithDeltas(inputHistory[i].input, inputHistory.Count > i + 1 ? inputHistory[i + 1].input : inputHistory[i].input);

                var events = eventHistory.ItemAt(inputHistory.TimeAt(i));

                if (events != null)
                    events?.Invoke(movement, false);

                movement.TickMovement(inputHistory[i].deltaTime, inputWithDeltas, true);

                // Draw debug info
                if (debugDrawReconciles && i > 0)
                {
                    // draw debug
                    MakeMoveState().DebugDraw(Color.blue);
                    stateHistory[i - 1].DebugDraw(Color.red);
                }
            }
        }

        if (extrapolateLocalInput)
        {
            lastConfirmedState = MakeMoveState();

            float extrapolatedDelta = Mathf.Min(clientTime - (inputHistory.LatestTime + inputHistory.Latest.deltaTime), maxDeltaTime);
            movement.TickMovement(extrapolatedDelta, PlayerInput.MakeWithDeltas(inputHistory.Latest.input, inputHistory.Latest.input), true);

            currentExtrapolation = extrapolatedDelta;
        }
        else
        {
            currentExtrapolation = 0f;
        }
    }

    public void CallEvent(MovementEvent eventToCall)
    {
        pendingEvents += eventToCall;
    }

    private void OnValidate()
    {
        if (limitInputRate)
        {
            maxInputRate = Mathf.Max(maxInputRate, 1);
            sendBufferLength = Mathf.Max(sendBufferLength, 1f / maxInputRate);
            remotePredictionTimeStep = Mathf.Max(remotePredictionTimeStep, 1f / maxInputRate); // no point in doing smaller time steps for predictions
        }
    }
}
