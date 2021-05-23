using System;
using UnityEngine;

public delegate void TickerEvent(bool isRealtime);

public class Ticker : MonoBehaviour
{
    [Flags]
    public enum SeekFlags
    {
        None = 0,

        /// <summary>
        /// Specifies that inputs should not use deltas. Useful when the full input history is not known, meaning a delta may not necessarily be correct
        /// 
        /// For example, a client receives an input for T=0 and T=1. At T=1 they extrapoalte the state noting that Jump has been pressed since T=0.
        /// However, they are unaware that jump was actually first pressed at T=0.5, and the state they received for T=1 has already jumped
        /// On the server, T=1 had no jump delta as T=0.5 already did that. On the client, T=1 is thought to have the jump delta despite being untrue, leading to inconsistency.
        /// </summary>
        IgnoreDeltas = 1
    };

    [Header("Tick")]
    [Tooltip("The maximum delta time to pass to tickable components")]
    public float maxDeltaTime = 0.03f;
    [Tooltip("The maximum amount of iterations we can run while extrapolating")]
    public int maxSeekIterations = 15;

    [Header("Input")]
    [Tooltip("The maximum input rate in hz. If <=0, the input rate is unlimited. This should be restricted sensibly so that clients do not send too many inputs and save CPU.")]
    public int maxInputRate = 60;

    [Header("Reconciling")]
    [Tooltip("Whether to reconcile even if the server's confirmed state matched the local state at the time")]
    public bool alwaysReconcile = false;

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
    /// The realtime playback during the last Seek. This is mainly for debugging and doesn't affect current state
    /// </summary>
    public float realtimePlaybackTime { get; private set; }

    /// <summary>
    /// The current confirmed playback time - the non-extrapolated playback time of the last input-confirmed state
    /// </summary>
    public float confirmedPlaybackTime { get; private set; }

    public float timeOfLastInputPush { get; private set; }

    public readonly HistoryList<PlayerInput> inputHistory = new HistoryList<PlayerInput>();

    private readonly HistoryList<TickerEvent> eventHistory = new HistoryList<TickerEvent>();

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

        timeOfLastInputPush = Time.time;
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

        timeOfLastInputPush = Time.time;
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
        int index = stateHistory.IndexAt(time, 0.0001f);
        if (alwaysReconcile || index == -1 || !stateHistory[index].Equals(state))
        {
            GetComponent<Character>().ApplyState(state);
            lastConfirmedState = state;
            confirmedPlaybackTime = time;
            playbackTime = time;
        }
    }

    /// <summary>
    /// Confirms the current character state. Needed to teleport or otherwise influence movement (except where events are used)
    /// </summary>
    public void ConfirmCurrentState()
    {
        lastConfirmedState = MakeState();
        confirmedPlaybackTime = playbackTime;
    }

    /// <summary>
    /// Applies a saved state package to the real state. Does not change lastConfirmedState which you may want to modify manually
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
    public void SeekBy(float deltaTime, float realtimePlaybackTime)
    {
        Seek(playbackTime + deltaTime, realtimePlaybackTime);
    }

    /// <summary>
    /// Ticks the player FORWARD only until the targetTime is reached, if possible
    /// This uses replayed inputs if those are available, otherwise extrapolates in steps up to maxDeltaTime length
    /// </summary>
    public void Seek(float targetTime, float realtimePlaybackTime, SeekFlags flags = SeekFlags.None)
    {
        Debug.Assert(maxDeltaTime > 0f);

        float initialPlaybackTime = playbackTime;

        // Restore our actual non-extrapolated position
        ApplyState(lastConfirmedState);
        playbackTime = confirmedPlaybackTime;

        // Playback our latest movements
        try
        {
            CharacterMovement movement = GetComponent<CharacterMovement>();
            int numIterations = 0;

            // Execute ticks, grabbing and consuming inputs if they are available, or using the latest inputs
            while (playbackTime < targetTime)
            {
                int index = inputHistory.ClosestIndexBefore(playbackTime, 0.001f);
                PlayerInput input = default;
                float deltaTime = Mathf.Min(targetTime - playbackTime, maxDeltaTime);
                bool canConfirmState = false;
                bool isRealtime = false;

                if (index == -1)
                {
                    // Sometimes we fall behind of the inputs and only a crazy amount of extrapolation would get us there
                    // That is bad, but we have a half-baked solution which is to just skip to the oldest input we have and start from there. We need to get there somehow.
                    int nextAfter = inputHistory.ClosestIndexAfter(playbackTime, 0.001f);

                    if (nextAfter != -1 && inputHistory.TimeAt(nextAfter) < targetTime)
                    {
                        index = nextAfter;
                        playbackTime = inputHistory.TimeAt(nextAfter);
                    }
                }

                if (index != -1)
                {
                    float inputTime = inputHistory.TimeAt(index);

                    if (index > 0)
                    {
                        float inputDeltaTime = inputHistory.TimeAt(index - 1) - inputHistory.TimeAt(index);

                        deltaTime = Mathf.Min(inputDeltaTime, targetTime - playbackTime);

                        // a tick is confirmed if we can do a full previous->current input tick
                        canConfirmState = (deltaTime == inputDeltaTime);
                    }

                    // use a delta if it's the crossing the beginning part of the input, otherwise extrapolate without delta
                    if (index + 1 < inputHistory.Count && playbackTime <= inputTime && playbackTime + deltaTime > inputTime && (flags & SeekFlags.IgnoreDeltas) == 0)
                        input = inputHistory[index].WithDeltas(inputHistory[index + 1]);
                    else
                        input = inputHistory[index].WithoutDeltas();

                    // on the server, the true non-reconciled state is the one that uses full inputs
                    // on the client, the same is true except when replaying things we've already played - i.e. Reconciles - and we pass forceReconciliation for that.
                    if (canConfirmState && playbackTime >= realtimePlaybackTime)
                        isRealtime = true;
                }
                else
                {
                    input = inputHistory.Latest.WithoutDeltas();
                }

                if (deltaTime > 0f)
                {
                    // invoke events
                    for (int i = 0; i < eventHistory.Count; i++)
                    {
                        if (eventHistory.TimeAt(i) >= playbackTime && eventHistory.TimeAt(i) < playbackTime + deltaTime)
                            eventHistory[i]?.Invoke(isRealtime);
                    }

                    // run a tick
                    movement.TickMovement(deltaTime, input, isRealtime);

                    playbackTime += deltaTime;

                    if (canConfirmState)
                    {
                        CharacterState state = MakeState();
                        stateHistory.Set(playbackTime, state);

                        // since this tick is a complete one, save the result as our next confirmed state
                        lastConfirmedState = state;
                        confirmedPlaybackTime = playbackTime;
                    }
                }

                numIterations++;

                if (numIterations == maxSeekIterations)
                {
                    Debug.LogWarning($"Max extrapolation iteration limit was hit whilst seeking ticker {gameObject.name}! It {numIterations} Target: {targetTime.ToString("F2")} playback: {playbackTime.ToString("F2")} initial {initialPlaybackTime.ToString("F2")} confirmed {confirmedPlaybackTime}");
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.WriteException(e);
        }

        // even if something went wrong, we prefer to say we're at the target time
        playbackTime = targetTime;
        this.realtimePlaybackTime = realtimePlaybackTime;

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
        Seek(originalPlaybackTime, originalPlaybackTime);
    }

    /// <summary>
    /// Draws the current character state
    /// </summary>
    public void DebugDrawCurrentState(Color colour)
    {
        GetComponent<Character>().MakeState().DebugDraw(colour);
    }

    /// <summary>
    /// Calls an event for the current playback time
    /// </summary>
    public void CallEvent(TickerEvent eventToCall)
    {
        int currentIndex = eventHistory.IndexAt(playbackTime, 0f);

        if (currentIndex != -1)
        {
            eventHistory[currentIndex] += eventToCall;
        }
        else
        {
            eventHistory.Set(playbackTime, eventToCall, 0f);
        }
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
