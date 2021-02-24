using Mirror;
using System;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Serializable]
    public struct MoveState : IEquatable<MoveState>
    {
        public float time;
        public float extrapolation;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 up;
        public CharacterMovement.State state;

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteSingle(time);
            writer.WriteUInt16(Compressor.CompressFloat16(extrapolation, 0f, 2f));
            writer.WriteVector3(position);
            writer.WriteInt32(Compressor.CompressQuaternion(rotation));
            writer.WriteUInt16(Compressor.CompressFloat16(velocity.x, -100f, 100f));
            writer.WriteUInt16(Compressor.CompressFloat16(velocity.y, -100f, 100f));
            writer.WriteUInt16(Compressor.CompressFloat16(velocity.z, -100f, 100f));
            writer.WriteInt16(Compressor.CompressNormal16(up));
            writer.Write((byte)state);
        }

        public void Deserialize(NetworkReader reader)
        {
            time = reader.ReadSingle();
            extrapolation = Compressor.DecompressFloat16(reader.ReadUInt16(), 0f, 2f);
            position = reader.ReadVector3();
            rotation = Compressor.DecompressQuaternion(reader.ReadInt32());
            velocity = new Vector3(Compressor.DecompressFloat16(reader.ReadUInt16(), -100f, 100f), Compressor.DecompressFloat16(reader.ReadUInt16(), -100f, 100f), Compressor.DecompressFloat16(reader.ReadUInt16(), -100f, 100f));
            up = Compressor.DecompressNormal16(reader.ReadInt16());
            state = (CharacterMovement.State)reader.ReadByte();
        }

        public bool Equals(MoveState other)
        {
            return other.position == position && other.rotation == rotation && other.velocity == velocity && other.state == state;
        }
    }

    public struct InputDelta
    {
        public PlayerInput input;
        public float deltaTime;

        public InputDelta(in PlayerInput input, float deltaTime)
        {
            this.input = input;
            this.deltaTime = deltaTime;
        }
    }

    public struct InputPack
    {
        public float startTime;
        public InputDelta[] inputs;
        public MoveState moveState;
    }

    public struct MoveStateWithInput
    {
        public MoveState moveState;
        public PlayerInput input;
    }

    public delegate void MovementEvent(Movement target, bool isReconciliation);

    [Header("Local simulation")]
    [Range(0.1f, 1f)]
    public float pingTolerance = 1f;
    [Range(0.01f, 1f)]
    public float sendBufferLength = 0.2f;

    [Space]
    public bool limitInputRate = true;
    public bool limitUpdatesToInputRate = true;
    public int maxInputRate = 240;

    [Space]
    public bool extrapolateLocalInput = true;

    [Space]
    public float maxDeltaTime = 0.5f;

    [Header("Remote playback")]
    public bool extrapolateRemoteInput = true;

    [Range(0f, 1f)]
    public float maxRemotePrediction = 0.3f;

    [Range(0.01f, 0.5f)]
    public float remotePredictionTimeStep = 0.08f;

    [Header("Reconcilation")]
    public bool alwaysReconcile = false;
    public float reconcilationPositionTolerance = 0.3f;

    [Header("Debug")]
    public bool printReconcileInfo = false;

    public HistoryList<InputDelta> inputHistory = new HistoryList<InputDelta>();

    public float clientPlaybackTime { get; private set; }
    public float clientTime { get; private set; }
    public float currentExtrapolation { get; private set; }

    private MovementEvent pendingEvents = null;
    private HistoryList<MovementEvent> eventHistory = new HistoryList<MovementEvent>();

    private HistoryList<Vector3> positionHistory = new HistoryList<Vector3>();

    private bool hasReceivedMoveState = false;
    private MoveState lastReceivedMoveState;
    private PlayerInput lastReceivedMoveStateInput; // valid on client when receiving a player from the server

    private float nextInputUpdateTime;

    private Player player;
    private CharacterMovement movement;

    private void Awake()
    {
        player = GetComponent<Player>();
        movement = GetComponent<CharacterMovement>();
    }


    private void Start()
    {
        lastConfirmedState = MakeMoveState();
    }

    /// <summary>
    /// Ticks the character. Received messages are processed during this phase
    /// </summary>
    public void Tick()
    {
        // Receive latest networked inputs/states etc
        // performs reconciliations and stuff
        ProcessReceivedStates();

        // In this example, your position at time=0 is the position you were at BEFORE inputs were applied.
        // Then the inputs at time=0 were applied, your movement occured and your position at time=0.1 is the result.
        UpdateLocalInputs();

        // Tick movement
        RunTicks();
    }

    public void PushInput(PlayerInput input, float delta)
    {
        inputHistory.Set(Mathf.Max(clientPlaybackTime, clientTime - maxDeltaTime), new InputDelta(input, Mathf.Min(delta, maxDeltaTime)));
    }

    private void UpdateLocalInputs()
    {
        if (hasAuthority)
            clientTime += Time.deltaTime;

        if (hasAuthority && (!limitInputRate || clientTime >= nextInputUpdateTime))
        {
            // Add current player input to input history
            inputHistory.Insert(Mathf.Max(clientPlaybackTime, clientTime - maxDeltaTime), new InputDelta(player.input, Mathf.Min(clientTime - clientPlaybackTime, maxDeltaTime)));
            nextInputUpdateTime = clientTime + 1f / maxInputRate;
            positionHistory.Insert(Mathf.Max(clientPlaybackTime, clientTime - maxDeltaTime), transform.position);
        }

        float inputHistoryMaxLength = pingTolerance + Mathf.Min(1f / (Mathf.Min(Netplay.singleton.playerTickrate, limitInputRate ? maxInputRate : 1000f)), 5f);
        float trimTo = Mathf.Max(clientTime, clientPlaybackTime) - inputHistoryMaxLength;

        inputHistory.Prune(trimTo);
        eventHistory.Prune(trimTo);
        positionHistory.Prune(trimTo);
    }

    private float lastPlaybackInput = -1f;

    MoveState lastConfirmedState;

    private void RunTicks()
    {
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

    private void ProcessReceivedStates()
    {
        if (hasReceivedMoveState && !NetworkServer.active)
        {
            // validate this player (or our own) movement on the client
            ClientValidateMovement(lastReceivedMoveState, lastReceivedMoveStateInput);
        }

        hasReceivedMoveState = false;
    }

    public void ReceiveMovement(MoveState moveState, PlayerInput input = default)
    {
        if (moveState.time > lastReceivedMoveState.time) // don't receive out-of-order movements
        {
            lastReceivedMoveState = moveState;
            lastReceivedMoveStateInput = input;
            hasReceivedMoveState = true;
        }
    }

    public void ReceiveInputPack(InputPack inputPack)
    {
        float time = inputPack.startTime;
        for (int i = inputPack.inputs.Length - 1; i >= 0; i--)
        {
            inputHistory.Set(time, inputPack.inputs[i], 0.001f);
            time += inputPack.inputs[i].deltaTime;
        }

        ReceiveMovement(inputPack.moveState);

        clientTime = time;
        player.input = inputHistory.Latest.input;
    }

    public PlayerInput GetLatestInput()
    {
        return inputHistory.Latest.input;
    }

    private void ClientValidateMovement(MoveState moveState, PlayerInput input)
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
            float predictionAmount = Mathf.Min(maxRemotePrediction, PlayerTicker.singleton ? PlayerTicker.singleton.localPlayerPing : 0f);

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

    private void TryReconcile(MoveState pastState)
    {
        Vector3 position = transform.position;
        Debug.Assert(hasAuthority && !NetworkServer.active);

        // hop back to server position
        ApplyMoveState(pastState);

        // fast forward if we can
        int startState = inputHistory.ClosestIndexBefore(pastState.time);
        string history = "";

        if (startState != -1)
        {
            // resimulate to our latest position
            float t = inputHistory.TimeAt(startState);
            for (int i = startState; i >= 0; i--)
            {
                PlayerInput inputWithDeltas = PlayerInput.MakeWithDeltas(inputHistory[i].input, inputHistory.Count > i + 1 ? inputHistory[i + 1].input : inputHistory[i].input);

                if (printReconcileInfo)
                {
                    history += $"\n-> {i}@{t:F2}: {inputWithDeltas} offset: {transform.position - positionHistory[i]}";
                    t += inputHistory[i].deltaTime;
                }

                var events = eventHistory.ItemAt(inputHistory.TimeAt(i));

                if (events != null)
                    events?.Invoke(movement, false);

                movement.TickMovement(inputHistory[i].deltaTime, inputWithDeltas, true);
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

        if (printReconcileInfo)
        {
            history += $"\nfinal offset: {transform.position - position}";
            Log.Write($"Reconcile: {pastState.time:F2} ({startState}@{(startState != -1 ? inputHistory.TimeAt(startState) : -1):F2}) clientTime: {clientTime:F2} {history}");
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

    public InputPack MakeInputPack()
    {
        int startIndex = inputHistory.ClosestIndexBeforeOrEarliest(clientTime - sendBufferLength);

        if (startIndex != -1)
        {
            InputDelta[] inputs = new InputDelta[startIndex + 1];
            for (int i = startIndex; i >= 0; i--)
                inputs[i] = inputHistory[i];

            return new InputPack()
            {
                startTime = inputHistory.TimeAt(startIndex),
                inputs = inputs
            };
        }

        return new InputPack()
        {
            startTime = inputHistory.LatestTime,
            inputs = new InputDelta[0]
        };
    }

    /// <summary>
    /// Makes a move state from our current state
    /// </summary>
    public MoveState MakeMoveState()
    {
        return new MoveState()
        {
            time = clientPlaybackTime,
            extrapolation = currentExtrapolation,
            position = transform.position,
            rotation = transform.rotation,
            state = movement.state,
            velocity = movement.velocity,
            up = movement.up
        };
    }

    /// <summary>
    /// Returns the last confirmed move state or erm thing
    /// </summary>
    public MoveState MakeOrGetConfirmedMoveState()
    {
        bool doExtrapolate = hasAuthority && extrapolateLocalInput;
        bool doRemoteExtrapolate = !hasAuthority && extrapolateRemoteInput;

        if (doExtrapolate || doRemoteExtrapolate)
        {
            lastConfirmedState.extrapolation = currentExtrapolation;
            return lastConfirmedState;
        }
        else
        {
            return MakeMoveState();
        }
    }

    public void ApplyMoveState(MoveState state)
    {
        transform.position = state.position;
        transform.rotation = state.rotation;
        movement.state = state.state;
        movement.velocity = state.velocity;
        movement.up = state.up;

        //Physics.SyncTransforms(); // CRUCIAL for correct collision checking - a lot of things broke before adding this...
    }

    public static string GetDebugInfo()
    {
        PlayerController player = Netplay.singleton.players[1]?.GetComponent<PlayerController>();

        if (player)
        {
            string output = $"clientTime: {player.clientTime:F2}\nclientPlaybackTime: {player.clientPlaybackTime:F2}\n";

            for (int i = 0; i < player.inputHistory.Count; i++)
                output += $"{i}@{player.inputHistory.TimeAt(i):F2} dt {player.inputHistory[i].deltaTime:F2} ctl: {player.inputHistory[i].input}\n";

            return output;
        }
        return "no player found";
    }
}

public static class MoveStateReaderWriter
{
    public static void WriteMoveState(this NetworkWriter writer, PlayerController.MoveState state)
    {
        state.Serialize(writer);
    }

    public static PlayerController.MoveState ReadMoveState(this NetworkReader reader)
    {
        PlayerController.MoveState state = default;
        state.Deserialize(reader);
        return state;
    }
}