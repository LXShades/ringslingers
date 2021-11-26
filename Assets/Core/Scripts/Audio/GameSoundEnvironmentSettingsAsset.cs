using UnityEngine;

[CreateAssetMenu(fileName = "SoundEnvironmentSettings", menuName = "Sound Environment Settings")]
public class GameSoundEnvironmentSettingsAsset : ScriptableObject
{
    public GameSoundEnvironmentSettings value;
}

[System.Serializable]
public class GameSoundEnvironmentSettings
{
    public static GameSoundEnvironmentSettings Default = new GameSoundEnvironmentSettings();

    [Tooltip("Adds this value to sounds if they are being played from an external source rather than the listener"), Range(-60f, 0f)]
    public float externalVolumeModifier = 0f;

    [Tooltip("The range at which the sound is at the loudest")]
    public float minRange = 0f;

    [Tooltip("The range at which the sound is half as loud")]
    public float midRange = 8f;

    [Tooltip("The range at which the sound falls silent")]
    public float maxRange = 20f;
}
