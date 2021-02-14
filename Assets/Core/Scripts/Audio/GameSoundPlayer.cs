using UnityEngine;

/// <summary>
/// Automatically plays/repeats sound effects
/// </summary>
public class GameSoundPlayer : MonoBehaviour
{
    public bool playOnStart = false;

    public bool isLooping = false;

    public bool isRepeating = false;
    public GameSound soundToPlay = new GameSound();

    [Range(0, 60)]
    public float repeatIntervalMin;
    [Range(0, 60)]
    public float repeatIntervalMax;

    private float nextPlayTime = 0.0f;

    private void Start()
    {
        if (playOnStart)
        {
            GameSounds.PlaySound(gameObject, soundToPlay);
        }
    }

    private void Update()
    {
        if (isRepeating)
        {
            if (Time.time >= nextPlayTime)
            {
                GameSounds.PlaySound(gameObject, soundToPlay);

                nextPlayTime = Time.time + Random.Range(repeatIntervalMin, repeatIntervalMax);
            }
        }
    }

    public void Play()
    {
        GameSounds.PlaySound(gameObject, soundToPlay);
    }
}
