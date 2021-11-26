using UnityEngine;

/// <summary>
/// Manages a simple sound object system where you can play sounds with random pitch variation source attaching/detaching, etc
/// </summary>
public class GameSounds : MonoBehaviour
{
    public static GameSounds singleton { get;  private set; }

    /// <summary>
    /// Prefab containing the audio source with settings etc
    /// </summary>
    public GameObject audioSourcePrefab;

    /// <summary>
    /// Number of concurrent audio sources that can run at once
    /// </summary>
    public int numSoundChannels = 32;

    public bool enableSounds = true;

    /// <summary>
    /// List of available audio sources
    /// </summary>
    private AudioSource[] sources;

    private GameObject[] sourceAttachments;

    private AudioListener listener
    {
        get
        {
            if (_listener == null)
                _listener = FindObjectOfType<AudioListener>();

            return _listener;
        }
    }
    private AudioListener _listener;

    private void Awake()
    {
        if (singleton)
        {
            Destroy(gameObject);
            return;
        }

        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
        singleton = this;
        sources = new AudioSource[numSoundChannels];
        sourceAttachments = new GameObject[numSoundChannels];

        Debug.Assert(audioSourcePrefab && audioSourcePrefab.GetComponent<AudioSource>());
        for (int i = 0; i < numSoundChannels; i++)
        {
            sources[i] = Instantiate(audioSourcePrefab, transform).GetComponent<AudioSource>();
            sources[i].transform.SetParent(transform);
        }
    }

    private void LateUpdate()
    {
        if (!enableSounds)
            return;

        // We can't just attach them because if the objects get destroyed, so does the source. So we'll just move the sources around
        for (int i = 0; i < numSoundChannels; i++)
        {
            if (sourceAttachments[i] != null)
                sources[i].transform.position = sourceAttachments[i].transform.position;
        }
    }

    public static void PlaySound(GameObject source, GameSound sound)
    {
        if (singleton)
        {
            singleton.InternalPlaySound(source, sound, new SoundOverrides(0f));
        }
    }

    public static void PlaySound(GameObject source, GameSound sound, in SoundOverrides overrides)
    {
        if (singleton)
        {
            singleton.InternalPlaySound(source, sound, overrides);
        }
    }

    public static void PlaySound(Vector3 position, GameSound sound)
    {
        if (singleton)
        {
            singleton.InternalPlaySound(null, sound, new SoundOverrides(0f), true, position);
        }
    }

    public static void PlaySound(Vector3 position, GameSound sound, in SoundOverrides overrides)
    {
        if (singleton)
        {
            singleton.InternalPlaySound(null, sound, overrides, true, position);
        }
    }

    AnimationCurve currentRolloffCurve = new AnimationCurve();

    private void InternalPlaySound(GameObject sourceObject, GameSound sound, in SoundOverrides overrides, bool hasRawPosition = false, Vector3 rawPosition = default)
    {
        if (sound == null || sound.clip == null || sound.volumeDecibels <= -60f || listener == null || !enableSounds)
            return;

        // Pick a clip to play
        int indexToPlay = 0;
        AudioClip clipToPlay;

        if (overrides.randomSoundIndex < 0)
        {
            if (sound.additionalClips != null && sound.additionalClips.Length > 0)
            {
                indexToPlay = Random.Range(0, sound.additionalClips.Length + 1);
            }
        }
        else
        {
            indexToPlay = Mathf.Clamp(overrides.randomSoundIndex, 0, sound.additionalClips.Length + 1);
        }

        clipToPlay = indexToPlay == 0 ? sound.clip : sound.additionalClips[indexToPlay - 1];

        // Find the best channel
        int bestChannel = -1;
        float bestChannelExistingVolume = 0f;
        for (int i = 0; i < sources.Length; i++)
        {
            if (!sources[i].isPlaying)
            {
                bestChannel = i;
                break;
            }
        }

        if (bestChannel == -1)
        {
            Vector3 listenerPosition = listener.transform.position;

            bestChannelExistingVolume = float.MaxValue;

            for (int i = 0; i < sources.Length; i++)
            {
                float vol = sources[i].spatialBlend > 0.5f ? 
                    sources[i].GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(Vector3.Distance(listenerPosition, sources[i].transform.position)) :
                    1f;
                if (vol < bestChannelExistingVolume)
                {
                    bestChannelExistingVolume = vol;
                    bestChannel = i;
                }
            }
        }

        AudioSource player = sources[bestChannel];
        GameSoundEnvironmentSettings environment = sound.environment != null ? sound.environment.value : GameSoundEnvironmentSettings.Default;
        Vector3 effectivePosition = sourceObject ? sourceObject.transform.position : rawPosition;
        float spatialBlend = environment.maxRange > 0f && (sourceObject || hasRawPosition) ? 1f : 0f;

        for (int i = currentRolloffCurve.length - 1; i >= 0; i--)
            currentRolloffCurve.RemoveKey(i);

        currentRolloffCurve.AddKey(new Keyframe(environment.minRange, 1f));
        currentRolloffCurve.AddKey(new Keyframe(environment.midRange, 0.3162f));
        currentRolloffCurve.AddKey(new Keyframe(environment.maxRange, 0f));
        currentRolloffCurve.SmoothTangents(0, 0f);
        currentRolloffCurve.SmoothTangents(1, 0f);
        currentRolloffCurve.SmoothTangents(2, 1f);

        if (sources[bestChannel].isPlaying) // if we're replacing another sound, make sure we're more important (in this case just louder)
        {
            float totalVolume = DbToAmplitude(sound.volumeDecibels + overrides.volumeModifier);
            if (spatialBlend > 0.5f)
                totalVolume *= currentRolloffCurve.Evaluate(Vector3.Distance(effectivePosition, listener.transform.position));

            if (totalVolume < bestChannelExistingVolume)
                return; // don't go ahead, this sound is too quiet / not worth it
        }

        player.transform.position = effectivePosition;
        player.clip = clipToPlay;
        player.volume = DbToAmplitude(sound.volumeDecibels + overrides.volumeModifier);
        player.pitch = sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance);
        player.spatialBlend = spatialBlend;

        player.minDistance = environment.minRange;
        player.maxDistance = environment.maxRange;

        player.rolloffMode = AudioRolloffMode.Custom;
        player.SetCustomCurve(AudioSourceCurveType.CustomRolloff, currentRolloffCurve);

        sourceAttachments[bestChannel] = sourceObject;
        player.Play();
    }

    public static bool IsSoundPlayingOn(GameObject target)
    {
        return singleton.InternalIsSoundPlaying(target);
    }

    private bool InternalIsSoundPlaying(GameObject target)
    {
        for (int i = 0; i < numSoundChannels; i++)
        {
            if (sourceAttachments[i] == target && sources[i].isPlaying)
            {
                return true;
            }
        }

        return false;
    }

    public float AmplitudeToDb(float amplitude) => 20.0f * Mathf.Log10(amplitude);
    public float DbToAmplitude(float db) => Mathf.Pow(10.0f, db / 20.0f);
}

/// <summary>
/// Sound effect container with mixing options
/// </summary
[System.Serializable]
public class GameSound
{
    [Tooltip("The clip to play")]
    public AudioClip clip = null;

    [Tooltip("Alternative clips to play if applicable")]
    public AudioClip[] additionalClips = null;

    [Tooltip("When 3D, how the sound is affected by environmental effects. Does not apply to sounds played directly on the listener.")]
    public GameSoundEnvironmentSettingsAsset environment;

    [Tooltip("Default volume to play the clip at, in dB, where -60 is silence"), Range(-60f, 0f)]
    public float volumeDecibels = 0f;

    [Tooltip("Default pitch to play the clip at")]
    public float pitch = 1;

    [Tooltip("Random higher and lower pitch variance when playing the sound")]
    public float pitchVariance = 0;

    [HideInInspector] public bool looping; // not implemented, just protection from future errors (trust me, me.) UPDATE: it's now Nov 2021, it's me, and ngl, I have no idea what I did and why I should trust it

    public GameSound Clone() => (GameSound)MemberwiseClone();
}

public struct SoundOverrides
{
    public SoundOverrides(float volumeModifier = 0f, int randomSoundIndex = -1)
    {
        this.volumeModifier = volumeModifier;
        this.randomSoundIndex = randomSoundIndex;
    }

    public int randomSoundIndex; // if -1, a random sound is used as normal
    public float volumeModifier; // added to the volume
}