using UnityEngine;

[System.Serializable]
public class LevelConfiguration
{
    [Header("Map info")]
    public string friendlyName;
    public string credits = "<credits missing>";

    [Header("TODO (not used yet)")]
    public Sprite screenshot;

    [Header("Game mode")]
    [Tooltip("The default game mode to load for this scene")]
    public GameObject defaultGameModePrefab;
    public WeaponAmmoStyle defaultWeaponAmmoStyle = WeaponAmmoStyle.Time;
    public WeaponCombinationStyle defaultWeaponCombinationStyle = WeaponCombinationStyle.Combinable;

    [Header("Level settings")]
    public int defaultPlayerLimit = 12;

    [Tooltip("Whether to include the map in the automatic rotation")]
    public bool includeInRotation = true;
    [Tooltip("Whether the map should be included in map selection dropdowns")]
    public bool includeInMapSelection = true;

    [Tooltip("Minimum amount of players in the server for this to be considered in the rotation")]
    public int minRotationPlayers;

    [Tooltip("Maximum amount of players in the server fort his to be considered in the rotation")]
    public int maxRotationPlayers;

    [Header("Internal")]
    public string path = "";

    public void OnValidate()
    {
        maxRotationPlayers = Mathf.Min(maxRotationPlayers, defaultPlayerLimit);
        minRotationPlayers = Mathf.Max(minRotationPlayers, 0);
    }
}
