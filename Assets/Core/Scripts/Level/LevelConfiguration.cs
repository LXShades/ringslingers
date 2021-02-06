using UnityEngine;

[System.Serializable]
public class LevelConfiguration
{
    public string friendlyName;

    public Sprite mapShot;

    [Header("The default game mode to load for this scene")]
    public GameObject defaultGameModePrefab;

    public int defaultPlayerLimit = 12;

    [Header("Whether to include the map in the rotation")]
    public bool includeInRotation = false;

    [Tooltip("Minimum amount of players in the server for this to be considered in the rotation")]
    public int minRotationPlayers;

    [Tooltip("Maximum amount of players in the server fort his to be considered in the rotation")]
    public int maxRotationPlayers;

    public void OnValidate()
    {
        maxRotationPlayers = Mathf.Min(maxRotationPlayers, defaultPlayerLimit);
        minRotationPlayers = Mathf.Max(minRotationPlayers, 0);
    }
}
