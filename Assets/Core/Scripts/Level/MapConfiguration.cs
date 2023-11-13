using UnityEngine;

[System.Serializable]
public class MapConfiguration
{
    [Header("Map info")]
    public string friendlyName;
    public string credits = "<credits missing>";
    public Sprite screenshot;

    [Header("Game mode")]
    [Tooltip("The default game mode to load for this scene")]
    public GameState defaultGameModePrefab;
    public WeaponAmmoStyle defaultWeaponAmmoStyle = WeaponAmmoStyle.Time;
    public WeaponCombinationStyle defaultWeaponCombinationStyle = WeaponCombinationStyle.Combinable;

    [Header("Level settings")]
    public int defaultPlayerLimit = 12;

    [Header("TODO - not implemented yet")]
    [Tooltip("Minimum amount of players in the server for this to be considered in the rotation")]
    public int minRotationPlayers = 0;

    [Tooltip("Maximum amount of players in the server for this to be considered in the rotation")]
    public int maxRotationPlayers = 32;

    [Header("Dev")]
    [Tooltip("Whether the map should be hidden from the normal game outside of editor")]
    public bool isDevOnly = false;

    [Header("Internal")]
    public string path = "";

    public void OnValidate()
    {
        maxRotationPlayers = Mathf.Min(maxRotationPlayers, defaultPlayerLimit);
        minRotationPlayers = Mathf.Max(minRotationPlayers, 0);
    }
}
