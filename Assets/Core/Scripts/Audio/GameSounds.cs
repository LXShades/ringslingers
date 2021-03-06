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

    private int currentChannel = -1;

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

    private void InternalPlaySound(GameObject sourceObject, GameSound sound, in SoundOverrides overrides, bool rawPosition = false, Vector3 position = default)
    {
        if (sound == null || sound.clip == null || sound.volumeDecibels <= -60f || listener == null || !enableSounds)
            return;

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

        if (indexToPlay == 0)
            clipToPlay = sound.clip;
        else
            clipToPlay = sound.additionalClips[indexToPlay - 1];

        currentChannel = (currentChannel + 1) % sources.Length;

        for (int i = 0; i < sources.Length; i++)
        {
            if (!sources[currentChannel].isPlaying)
            {
                break;
            }
            else
            {
                currentChannel = (currentChannel + 1) % sources.Length;
            }
        }

        AudioSource player = sources[currentChannel];

        player.clip = clipToPlay;
        player.volume = DbToAmplitude(sound.volumeDecibels + overrides.volumeModifier);
        player.pitch = sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance);

        for (int i = currentRolloffCurve.length - 1; i >= 0; i--)
            currentRolloffCurve.RemoveKey(i);

        currentRolloffCurve.AddKey(new Keyframe(sound.minRange, 1f));
        currentRolloffCurve.AddKey(new Keyframe(sound.midRange, 0.3162f));
        currentRolloffCurve.AddKey(new Keyframe(sound.maxRange, 0f));
        currentRolloffCurve.SmoothTangents(0, 0.5f);
        currentRolloffCurve.SmoothTangents(1, 0.5f);
        currentRolloffCurve.SmoothTangents(2, 0.5f);
        
        player.minDistance = sound.minRange;
        player.maxDistance = sound.maxRange;

        player.rolloffMode = AudioRolloffMode.Custom;
        player.SetCustomCurve(AudioSourceCurveType.CustomRolloff, currentRolloffCurve);

        player.spatialBlend = sound.blend3D;
        
        if (sourceObject)
            player.transform.position = sourceObject.transform.position;
        else if (!rawPosition)
            player.spatialBlend = 0f;
        else if (rawPosition)
            player.transform.position = position;

        sourceAttachments[currentChannel] = sourceObject;
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
    public enum RangePreset
    {
        Custom = 0,
        EnemyImportant,
        EnemyNormal
    }

    [Tooltip("The clip to play")]
    public AudioClip clip = null;

    [Tooltip("Alternative clips to play if applicable")]
    public AudioClip[] additionalClips = null;

    [Tooltip("How 3D is the sound?")]
    public float blend3D = 1f;

    [Tooltip("A range preset. When changed, the sound is bound to the preset setting and all sounds will change their values to match")]
    public RangePreset rangePreset = RangePreset.Custom;

    [Tooltip("The range at which the sound is at the loudest")]
    public float minRange = 1f;

    [Tooltip("The range at which the sound is half as loud")]
    public float midRange = 10f;

    [Tooltip("The range at which the sound falls silent")]
    public float maxRange = 20f;

    [Tooltip("Default volume to play the clip at, in dB, where -60 is silence"), Range(-60f, 0f)]
    public float volumeDecibels = 0;

    [Tooltip("Default pitch to play the clip at")]
    public float pitch = 1;

    [Tooltip("Random higher and lower pitch variance when playing the sound")]
    public float pitchVariance = 0;

    [HideInInspector] public bool looping; // not implemented, just protection from future errors (trust me, me.)

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