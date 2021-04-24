using System;
using UnityEngine;

public class Ticker : MonoBehaviour
{
    [Header("Tick")]
    [Tooltip("The maximum delta time to pass to tickable components")]
    public float maxDeltaTime = 0.03f;
    [Tooltip("The maximum amount of iterations we can run while extrapolating")]
    public int maxSeekIterations = 15;

    [Header("Input")]
    [Tooltip("The maximum input rate in hz. If <=0, the input rate is unlimited. This should be restricted sensibly so that clients do not send too many inputs and save CPU.")]
    public int maxInputRate = 60;

    [Header("History")]
    [Tooltip("How long to keep input, state, etc history in seconds. Should be able to fit in a bit more ")]
    public float historyLength = 1.0f;

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

    /// <summary>
    /// Whether this ticker has an owning player's input
    /// </summary>
    public bool hasInput => inputHistory.Count > 0;

    /// <summary>
    /// The latest known input in this ticker
    /// </summary>
    public PlayerInput input => inputHistory.Latest.input;

    /// <summary>
    /// The current playback time, based on no specific point of reference, but is expected to use the same time format as input and event history
    /// </summary>
    public float playbackTime { get; private set; }

    /// <summary>
    /// The current confirmed playback time - the non-extrapolated playback time of the last input-confirmed state
    /// </summary>
    public float confirmedPlaybackTime { get; private set; }

    public readonly HistoryList<InputDelta> inputHistory = new HistoryList<InputDelta>();

    private readonly HistoryList<MovementEvent> eventHistory = new HistoryList<MovementEvent>();

    private readonly HistoryList<CharacterState> stateHistory = new HistoryList<CharacterState>();

    CharacterState lastConfirmedState;

    /// <summary>
    /// Refreshes the input, event and state history 
    /// </summary>
    public void PushInput(PlayerInput input, float time)
    {
        float timeSinceLastInput = time - inputHistory.LatestTime;

        // TODO: 
        if (timeSinceLastInput < -0.2f)
        {
            inputHistory.Clear();
            eventHistory.Clear();
            stateHistory.Clear();
            Debug.Log($"Clearing input history on {gameObject} because latest time {time} is earlier than {inputHistory.LatestTime} by {timeSinceLastInput} and a full reset was implied.");
        }

        if (maxInputRate <= 0 || timeSinceLastInput >= 1f / maxInputRate - 0.0001f)
        {
            // Add current player input to input history
            inputHistory.Set(time, new InputDelta(input, Mathf.Min(timeSinceLastInput, maxDeltaTime)));
        }

        CleanupHistory();
    }

    /// <summary>
    /// Stores an input pack into input history
    /// </summary>
    public void PushInputPack(InputPack inputPack)
    {
        float time = inputPack.startTime;

        for (int i = inputPack.inputs.Length - 1; i >= 0; i--)
        {
            // TODO: max input rate enforcement
            inputHistory.Set(time, inputPack.inputs[i], 0.001f);
            time += inputPack.inputs[i].deltaTime;
        }

        CleanupHistory();
    }

    /// <summary>
    /// Makes an input pack from input history
    /// </summary>
    public InputPack MakeInputPack(float maxLength)
    {
        return InputPack.MakeFromHistory(inputHistory, maxLength);
    }

    /// <summary>
    /// Loads a state and sets the playback time to that state
    /// </summary>
    /// <param name="time"></param>
    public void Rewind(CharacterState state, float time)
    {
        GetComponent<Character>().ApplyState(state);
        lastConfirmedState = state;
        confirmedPlaybackTime = time;
        playbackTime = time;
    }

    /// <summary>
    /// Ticks the player forward by the given deltaTime, if possible
    /// </summary>
    public void SeekBy(float deltaTime, bool isReconciliation)
    {
        Seek(playbackTime + deltaTime, isReconciliation);
    }

    /// <summary>
    /// Ticks the player FORWARD only until the targetTime is reached, if possible
    /// This uses replayed inputs if those are available
    /// </summary>
    public void Seek(float targetTime, bool isReconciliation)
    {
        // Playback our latest movements
        int index = inputHistory.ClosestIndexAfter(playbackTime, 0.001f);

        Debug.Assert(maxDeltaTime > 0f);

        try
        {
            Character character = GetComponent<Character>();
            CharacterMovement movement = GetComponent<CharacterMovement>();

            // If possible, replay inputs until the target time is reached or almost reached
            // TODO: stop at targetTime, don't exceed, although we might never actually need that feature
            if (index != -1)
            {
                // Due to extrapolation, we need to restore our actual non-extrapolated position
                character.ApplyState(lastConfirmedState);

                if (debugDrawReconciles && isReconciliation)
                {
                    character.MakeState().DebugDraw(Color.blue);
                    stateHistory[index].DebugDraw(Color.red);
                }

                // Execute the ticks up to this point
                for (int i = index; i >= 0; i--)
                {
                    float time = inputHistory.TimeAt(i);
                    var events = eventHistory.ItemAt(time);
                    PlayerInput inputWithDeltas = PlayerInput.MakeWithDeltas(inputHistory[i].input, inputHistory.Count > i + 1 ? inputHistory[i + 1].input : inputHistory[i].input);

                    if (events != null)
                        events?.Invoke(false);

                    movement.TickMovement(inputHistory[i].deltaTime, inputWithDeltas, isReconciliation);
                    playbackTime += inputHistory[i].deltaTime;

                    // Draw debug info
                    if (debugDrawReconciles && i > 0)
                    {
                        character.MakeState().DebugDraw(Color.blue);
                        stateHistory[i - 1].DebugDraw(Color.red);
                    }
                }

                playbackTime = inputHistory.LatestTime + inputHistory.Latest.deltaTime; // precision correction

                lastConfirmedState = character.MakeState();
                confirmedPlaybackTime = playbackTime;
            }

            // Then extrapolate beyond original time, constrained to maxTimeStep and maxExtrapolationIterations
            if (targetTime > playbackTime)
            {
                int numIterations;

                for (numIterations = 0; numIterations < maxSeekIterations; numIterations++)
                {
                    if (playbackTime < targetTime)
                    {
                        float deltaTime = Mathf.Min(targetTime - playbackTime, maxDeltaTime);

                        movement.TickMovement(deltaTime, inputHistory.Latest.input, true);
                        playbackTime += deltaTime;
                    }
                    else break;
                }

                if (numIterations == maxSeekIterations)
                {
                    Debug.LogWarning($"Max extrapolation iteration limit was hit whilst seeking ticker {gameObject.name}! It {numIterations} Target {targetTime} playback {playbackTime}");
                    playbackTime = targetTime; // skip anyway
                }
            }
        }
        catch (Exception e)
        {
            Log.WriteException(e);
        }

        // Perform history cleanup and stuff
        SaveStateSnapshot();
        CleanupHistory();
    }

    /// <summary>
    /// Rewinds to pastState and fast forwards to the present time, using recorded inputs if available
    /// </summary>
    public void Reconcile(CharacterState pastState, float pastStateTime)
    {
        float originalPlaybackTime = playbackTime;

        Rewind(pastState, pastStateTime);
        Seek(originalPlaybackTime, true);
    }

    /// <summary>
    /// Prunes old history that shouldn't be needed anymore
    /// </summary>
    private void CleanupHistory()
    {
        float trimTo = playbackTime - historyLength;

        inputHistory.Prune(trimTo);
        eventHistory.Prune(trimTo);
        stateHistory.Prune(trimTo);
    }


    /// <summary>
    /// Saves a state into the StateHistory at the current playbackTime, replacing old ones if they exist
    /// </summary>
    private void SaveStateSnapshot()
    {
        Character character = GetComponent<Character>();
        stateHistory.Insert(playbackTime, character.MakeState());
    }
}
