using UnityEngine;

public class GameSounds : MonoBehaviour
{
    public static GameSounds singleton
    {
        get
        {
            if (_singleton == null)
            {
                _singleton = FindObjectOfType<GameSounds>();
            }

            return _singleton;
        }
    }
    private static GameSounds _singleton;

    /// <summary>
    /// Prefab containing the audio source with settings etc
    /// </summary>
    public GameObject audioSourcePrefab;

    /// <summary>
    /// Number of concurrent audio sources that can run at once
    /// </summary>
    public int numSoundChannels = 8;

    /// <summary>
    /// List of available audio sources
    /// </summary>
    private AudioSource[] sources;

    private GameObject[] sourceAttachments;

    private int currentChannel = -1;

    private void Awake()
    {
        sources = new AudioSource[numSoundChannels];
        sourceAttachments = new GameObject[numSoundChannels];

        Debug.Assert(audioSourcePrefab && audioSourcePrefab.GetComponent<AudioSource>());
        for (int i = 0; i < numSoundChannels; i++)
        {
            sources[i] = Instantiate(audioSourcePrefab, transform).GetComponent<AudioSource>();
        }
    }

    private void LateUpdate()
    {
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
            singleton.InternalPlaySound(source, sound);
        }
    }

    private void InternalPlaySound(GameObject source, GameSound sound)
    {
        if (sound == null || sound.clip == null || sound.volume <= 0)
            return;

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

        sources[currentChannel].clip = sound.clip;
        sources[currentChannel].volume = sound.volume;
        sources[currentChannel].pitch = sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance);
        sourceAttachments[currentChannel] = source;
        if (source)
        {
            sources[currentChannel].transform.position = source.transform.position;
            sources[currentChannel].spatialBlend = 1f;
        }
        else
        {
            sources[currentChannel].spatialBlend = 0f;
        }

        sources[currentChannel].Play();
    }
}

/// <summary>
/// Sound effect container with mixing options
/// </summary
[System.Serializable]
public class GameSound
{
    [Tooltip("The clip to play")]
    public AudioClip clip = null;

    [Tooltip("Default volume to play the clip at")]
    public float volume = 1;

    [Tooltip("Default pitch to play the clip at")]
    public float pitch = 1;

    [Tooltip("Random higher and lower pitch variance when playing the sound")]
    public float pitchVariance = 0;
}