using System.Collections;
using System.Collections.Generic;
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

    private int currentChannel = -1;

    private void Awake()
    {
        sources = new AudioSource[numSoundChannels];

        Debug.Assert(audioSourcePrefab && audioSourcePrefab.GetComponent<AudioSource>());
        for (int i = 0; i < numSoundChannels; i++)
        {
            sources[i] = Instantiate(audioSourcePrefab, transform).GetComponent<AudioSource>();
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
        sources[currentChannel].transform.SetParent(source.transform, false);
        sources[currentChannel].transform.localPosition = Vector3.zero;
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