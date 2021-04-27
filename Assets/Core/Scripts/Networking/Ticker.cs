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
    public PlayerInput input => inputHistory.Latest;

    /// <summary>
    /// The current playback time, based on no specific point of reference, but is expected to use the same time format as input and event history
    /// </summary>
    public float playbackTime { get; private set; }

    /// <summary>
    /// The current confirmed playback time - the non-extrapolated playback time of the last input-confirmed state
    /// </summary>
    public float confirmedPlaybackTime { get; private set; }

    public readonly HistoryList<PlayerInput> inputHistory = new HistoryList<PlayerInput>();

    private readonly HistoryList<MovementEvent> eventHistory = new HistoryList<MovementEvent>();

    private readonly HistoryList<CharacterState> stateHistory = new HistoryList<CharacterState>();

    public CharacterState lastConfirmedState { get; private set; }

    private void Awake()
    {
        lastConfirmedState = GetComponent<Character>().MakeState();
    }

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
            inputHistory.Set(time, input);
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
            inputHistory.Set(time, inputPack.inputs[i].input, 0.001f);
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
    /// Applies a saved state package to the real state
    /// </summary>
    private void ApplyState(CharacterState state)
    {
        GetComponent<Character>().ApplyState(state);
    }

    /// <summary>
    /// Makes a state package from the current state
    /// </summary>
    private CharacterState MakeState()
    {
        return GetComponent<Character>().MakeState();
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
        Debug.Assert(maxDeltaTime > 0f);

        // Restore our actual non-extrapolated position
        ApplyState(lastConfirmedState);
        playbackTime = confirmedPlaybackTime;

        // Playback our latest movements
        float initialPlaybackTime = playbackTime;

        try
        {
            CharacterMovement movement = GetComponent<CharacterMovement>();
            int numIterations = 0;

            // Execute ticks, grabbing and consuming inputs if they are available, or using the latest inputs
            while (playbackTime < targetTime)
            {
                int index = inputHistory.ClosestIndexBefore(playbackTime, 0.001f);
                PlayerInput input = default;
                MovementEvent events = null;
                float deltaTime = Mathf.Min(targetTime - playbackTime, maxDeltaTime);
                bool canConfirmState = false;

                if (index != -1)
                {
                    float inputTime = inputHistory.TimeAt(index);

                    events = eventHistory.ItemAt(inputTime);

                    if (inputHistory.Count > index + 1)
                        input = inputHistory[index].WithDeltas(inputHistory[index + 1]);
                    else
                        input = inputHistory[index].WithoutDeltas();

                    if (index > 0)
                    {
                        float inputDeltaTime = inputHistory.TimeAt(index - 1) - inputHistory.TimeAt(index);

                        deltaTime = Mathf.Min(inputDeltaTime, targetTime - playbackTime);
                        canConfirmState = (deltaTime == inputDeltaTime); // it's confirmed if we can do a full previous->current input tick
                    }
                }
                else
                {
                    input = inputHistory.Latest.WithoutDeltas();
                }

                if (deltaTime > 0f)
                {
                    events?.Invoke(isReconciliation);

                    movement.TickMovement(deltaTime, input, isReconciliation);

                    playbackTime += deltaTime;

                    if (canConfirmState)
                    {
                        // since this tick is complete, we can call it a confirmed tick
                        lastConfirmedState = MakeState();
                        confirmedPlaybackTime = playbackTime;
                    }
                }

                numIterations++;

                if (numIterations == maxSeekIterations)
                {
                    Debug.LogWarning($"Max extrapolation iteration limit was hit whilst seeking ticker {gameObject.name}! It {numIterations} Target {targetTime} playback {playbackTime}");
                    playbackTime = targetTime; // skip anyway
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.WriteException(e);
        }

        //Debug.Log($"SeekStat: {initialPlaybackTime.ToString("F2")}->{targetTime.ToString("F2")} final {playbackTime.ToString("F2")} dt: {(playbackTime - initialPlaybackTime).ToString("F2")}");

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
    /// Draws the current character state
    /// </summary>
    public void DebugDrawCurrentState(Color colour)
    {
        GetComponent<Character>().MakeState().DebugDraw(colour);
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
