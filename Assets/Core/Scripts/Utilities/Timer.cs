using UnityEngine;

/// <summary>
/// A robust timer class that can be started and stopped with a given duration
/// </summary>
public class Timer
{
    /// <summary>
    /// Returns the amount of time left before the timer expires, in seconds
    /// </summary>
    public float timeLeft => Mathf.Max(finishedTime - Time.time, 0.0f);

    /// <summary>
    /// Returns the time since the timer started, in seconds
    /// </summary>
    public float timeSinceStart => Time.time - startTime;

    /// <summary>
    /// The progress of the timer, starting at 0 and ending at 1 when the timer is finished
    /// </summary>
    public float progress
    {
        get
        {
            if (finishedTime - startTime > 0.0f)
            {
                return Mathf.Clamp01((Time.time - startTime) / (finishedTime - startTime));
            }
            else
            {
                return 1.0f;
            }
        }
    }

    /// <summary>
    /// Whether the timer is still ticking
    /// </summary>
    public bool isRunning => Time.time < finishedTime;

    // The Time.time value when this timer was last started
    public float startTime = 0.0f;

    // The Time.time value when this time will be finished
    public float finishedTime = 0.0f;

    /// <summary>
    /// Starts the timer with the given duration
    /// </summary>
    /// <param name="seconds">Number of seconds to count down from</param>
    public void Start(float seconds)
    {
        // Set the start and finished time
        startTime = Time.time;
        finishedTime = Time.time + seconds;
    }

    /// <summary>
    /// Starts the timer with no duration. (timeSinceStart can still be measured)
    /// </summary>
    public void Start()
    {
        startTime = Time.time;
        finishedTime = Time.time;
    }
}
