using Mirror;
using System;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Serializable]
    public struct MoveState : IEquatable<MoveState>
    {
        public float time;
        public Vector3 position;
        public Vector3 velocity;
        public CharacterMovement.State state;

        public bool Equals(MoveState other)
        {
            return other.position == position && other.velocity == velocity && other.state == state;
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

    public struct InputBuffer
    {
        public float startTime;
        public InputDelta[] inputs;
    }

    [Header("Debug")]
    public HistoryList<InputDelta> inputHistory = new HistoryList<InputDelta>();

    private HistoryList<Vector3> positionHistory = new HistoryList<Vector3>();

    public bool printReconcileInfo = false;

    [Header("Local simulation")]
    [Range(0.1f, 1f)]
    public float pingTolerance = 1f;
    [Range(0.1f, 1f)]
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
    [Range(0f, 1f)]
    public float maxRemotePrediction = 0.3f;

    [Range(0.01f, 0.5f)]
    public float remotePredictionTimeStep = 0.08f;

    [Header("Reconcilation")]
    public bool alwaysReconcile = false;
    public float reconcilationPositionTolerance = 0.3f;

    public float clientPlaybackTime { get; private set; }
    public float clientTime { get; private set; }

    private bool hasReceivedMoveState = false;
    private MoveState lastReceivedMoveState;

    private float nextInputUpdateTime;

    private Player player;
    private CharacterMovement movement;

    private void Start()
    {
        player = GetComponent<Player>();
        movement = GetComponent<CharacterMovement>();

        lastConfirmedState = MakeMoveState();
    }

    void Update()
    {
        // Receive latest networked inputs/states etc
        // performs reconciliations and stuff
        ProcessReceivedStates();

        // In this example, your position at time=0 is the position you were at BEFORE inputs were applied.
        // Then the inputs at time=0 were applied, your movement occured and your position at time=0.1 is the result.
        UpdateLocalInputs();

        // Tick movement
        RunTicks();

        // Send the movement state occasionally AFTER movement has executed
        if ((int)((Time.unscaledTime - Time.unscaledDeltaTime) * Netplay.singleton.playerTickrate) != (int)(Time.unscaledTime * Netplay.singleton.playerTickrate) 
            && (hasAuthority || NetworkServer.active))
        {
            SendMovementState();
        }
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
        inputHistory.Prune(clientTime - inputHistoryMaxLength);
        positionHistory.Prune(clientTime - inputHistoryMaxLength);
    }

    private float lastPlaybackInput = -1f;

    MoveState lastConfirmedState;

    private void RunTicks()
    {
        // Playback our latest movements
        int index = inputHistory.ClosestIndexAfter(lastPlaybackInput, 0f);
        bool doExtrapolate = hasAuthority && extrapolateLocalInput;

        try
        {
            if (index != -1)
            {
                if (doExtrapolate)
                    ApplyMoveState(lastConfirmedState); // when extrapolating our own inputs, we need to restore our positions for normal playback when the time comes

                for (int i = index; i >= 0; i--)
                {
                    movement.TickMovement(inputHistory[i].deltaTime, PlayerInput.MakeWithDeltas(inputHistory[i].input, inputHistory.Count > i + 1 ? inputHistory[i + 1].input : inputHistory[i].input), false);
                    lastPlaybackInput = inputHistory.TimeAt(i);
                }

                clientPlaybackTime = inputHistory.LatestTime + inputHistory.Latest.deltaTime; // precision correction

                if (doExtrapolate)
                    lastConfirmedState = MakeMoveState();
            }
            else if (doExtrapolate)
            {
                movement.TickMovement(Time.deltaTime, PlayerInput.MakeWithDeltas(player.input, player.input), true);
            }
        }
        catch (Exception e)
        {
            Log.WriteException(e);
        }
    }

    private void ProcessReceivedStates()
    {
        if (hasReceivedMoveState)
        {
            if (NetworkServer.active && !hasAuthority)
            {
                // validate this player's movement on the server
                ServerValidateMovement(lastReceivedMoveState);
            }
            else if (!NetworkServer.active)
            {
                // validate this player (or our own) movement on the client
                ClientValidateMovement(lastReceivedMoveState, default); // TODO
            }

            hasReceivedMoveState = false;
        }
    }

    private void SendMovementState()
    {
        // send inputs (and maybe final result?) to server
        if (hasAuthority && !NetworkServer.active)
        {
            int startIndex = inputHistory.ClosestIndexBeforeOrEarliest(clientTime - sendBufferLength);

            if (startIndex != -1)
            {
                InputBuffer buffer = new InputBuffer()
                {
                    startTime = inputHistory.TimeAt(startIndex),
                    inputs = new InputDelta[startIndex + 1]
                };

                for (int i = startIndex; i >= 0; i--)
                    buffer.inputs[i] = inputHistory[i];

                CmdMoveState(buffer, MakeMoveState());
            }
        }

        if (NetworkServer.active)
        {
            RpcMoveState(MakeMoveState());
        }
    }

    [Command(channel = Channels.DefaultUnreliable)]
    public void CmdMoveState(InputBuffer inputs, MoveState moveState)
    {
        float time = inputs.startTime;
        for (int i = inputs.inputs.Length - 1; i >= 0; i--)
        {
            inputHistory.Set(time, inputs.inputs[i], 0.001f);
            time += inputs.inputs[i].deltaTime;
        }

        OnReceivedMovement(moveState);

        clientTime = time;
    }

    [ClientRpc(channel = Channels.DefaultUnreliable, excludeOwner = true)]
    public void RpcMoveState(MoveState moveState)
    {
        if (NetworkServer.active)
            return;

        OnReceivedMovement(moveState);
    }

    /// <summary>
    /// Sent when the server confirms a different position for the client (or AlwaysReconcile is on)
    /// </summary>
    [TargetRpc(channel = Channels.DefaultUnreliable)]
    public void TargetReconcile(MoveState moveState)
    {
        OnReceivedMovement(moveState);
    }

    private void OnReceivedMovement(MoveState moveState)
    {
        if (moveState.time > lastReceivedMoveState.time) // don't receive out-of-order movements
        {
            lastReceivedMoveState = moveState;
            hasReceivedMoveState = true;
        }
    }

    private void ServerValidateMovement(MoveState moveState)
    {
        if (Vector3.Distance(moveState.position, transform.position) < reconcilationPositionTolerance && !alwaysReconcile)
        {
            ApplyMoveState(moveState);
        }
        else
        {
            TargetReconcile(MakeMoveState());
        }
    }

    private void ClientValidateMovement(MoveState moveState, PlayerInput input)
    {
        if (!hasAuthority)
        {
            // this is another player, just plop them here
            ApplyMoveState(moveState);
            inputHistory.Insert(moveState.time, new InputDelta(input, 0f)); // todo

            float predictionAmount = Netplay.singleton.unreliablePing;
            for (float t = 0; t < predictionAmount; t += remotePredictionTimeStep)
            {
                movement.TickMovement(Mathf.Min(remotePredictionTimeStep, predictionAmount - t), input, true);
            }
        }
        else
        {
            TryReconcile(moveState);
        }
    }

    private void TryReconcile(MoveState pastState)
    {
        Debug.Assert(hasAuthority && !NetworkServer.active);

        Vector3 position = transform.position;
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
                movement.TickMovement(inputHistory[i].deltaTime, inputWithDeltas, true);
            }
        }

        if (extrapolateLocalInput)
        {
            lastConfirmedState = MakeMoveState();

            float extrapolatedDelta = Mathf.Min(clientTime - (inputHistory.LatestTime + inputHistory.Latest.deltaTime), maxDeltaTime);
            movement.TickMovement(extrapolatedDelta, PlayerInput.MakeWithDeltas(inputHistory.Latest.input, inputHistory.Latest.input), true);
        }

        if (printReconcileInfo)
        {
            history += $"\nfinal offset: {transform.position - position}";
            Log.Write($"Reconcile: {pastState.time:F2} ({startState}@{(startState != -1 ? inputHistory.TimeAt(startState) : -1):F2}) clientTime: {clientTime:F2} {history}");
        }
    }

    private void OnValidate()
    {
        if (limitInputRate)
        {
            maxInputRate = Mathf.Max(maxInputRate, 1);
            sendBufferLength = Mathf.Max(sendBufferLength, 1f / maxInputRate);
        }
    }

    private MoveState MakeMoveState()
    {
        return new MoveState()
        {
            time = clientPlaybackTime,
            position = transform.position,
            state = movement.state,
            velocity = movement.velocity
        };
    }

    private void ApplyMoveState(MoveState state)
    {
        transform.position = state.position;
        movement.state = state.state;
        movement.velocity = state.velocity;

        Physics.SyncTransforms(); // CRUCIAL for correct collision checking - a lot of things broke before adding this...
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