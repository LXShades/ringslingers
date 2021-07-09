using System;

public interface ITickerBase
{
    public void Seek(float targetTime, float realtimePlaybackTime, TickerSeekFlags flags = TickerSeekFlags.None);
    public void SeekBy(float deltaTime, float realtimePlaybackTime);
}

public interface ITickerStateFunctions<TState> where TState : ITickerState<TState>
{
    public void Rewind(TState state, float time);
    public void Reconcile(TState pastState, float pastStateTime);
}

public interface ITickerInputFunctions<TInput> where TInput : ITickerInput<TInput>
{
    public void PushInput(TInput input, float time);
    public void PushInputPack(TickerInputPack<TInput> inputPack);
    public TickerInputPack<TInput> MakeInputPack(float maxLength);
}

public interface ITickableBase
{
    /// <summary>
    /// Should return a new Ticker with the TInput and TState used by this class
    /// </summary>
    ITickerBase CreateTicker();
} 

/// <summary>
/// Qualifies something as tickable.
/// 
/// This class can be simulated, reverted to a previous state, and "Seek" to an earlier _or_ future time in its history
/// 
/// Inputs are used to generate future times. Snapshots store earlier times. A Seek is able to revert to an earlier time, or extrapolate to a later time, based either on recorded inputs or the last known input.
/// 
/// This interface should implement MakeState(), ApplyState() and Tick().
/// </summary>
public interface ITickable<TInput, TState> : ITickableBase
{
    TState MakeState();

    void ApplyState(TState state);

    void Tick(float deltaTime, TInput input, bool isRealtime);
}


/// <summary>
/// Qualifies a struct or class as a ticker input.
/// 
/// The ticker input can be e.g a set of player controls specified by booleans (isJumpButtonDown) and floats (horizontalMovement).
/// 
/// This is sent to a tickable component's Simulate() function during a seek.
/// </summary>
public interface ITickerInput<TOwner>
{
    /// <summary>
    /// Returns an input with optional deltas compared to the previous input. e.g. btnJumpPressed, btnJumpReleased
    /// </summary>
    public TOwner WithDeltas(TOwner previousInput);

    /// <summary>
    /// Returns an input without any deltas (assuming no change from previous input)
    /// </summary>
    public TOwner WithoutDeltas();
}

/// <summary>
/// Qualifies a stuct or class as a ticker snapshot.
/// 
/// This is used to revert a tickable component to an earlier state for potential resimulation.
/// 
/// To work reliably, this snapshot should be able to store and load all simulatable state from a tickable component.
/// </summary>
public interface ITickerState<TState> : IEquatable<TState>
{
    void DebugDraw(UnityEngine.Color colour);
}

public struct TickerInputPack<TInput>
{
    public TInput[] inputs;
    public float[] times;

    /// <summary>
    /// Makes an InputPack
    /// </summary>
    /// <returns></returns>
    public static TickerInputPack<TInput> MakeFromHistory(HistoryList<TInput> inputHistory, float sendBufferLength)
    {
        int startIndex = inputHistory.ClosestIndexBeforeOrEarliest(inputHistory.LatestTime - sendBufferLength);

        if (startIndex != -1)
        {
            TInput[] inputs = new TInput[startIndex + 1];
            float[] times = new float[startIndex + 1];

            for (int i = startIndex; i >= 0; i--)
            {
                times[i] = inputHistory.TimeAt(i);
                inputs[i] = inputHistory[i];
            }

            return new TickerInputPack<TInput>()
            {
                inputs = inputs,
                times = times
            };
        }

        return new TickerInputPack<TInput>()
        {
            inputs = new TInput[0],
            times = new float[0]
        };
    }
}