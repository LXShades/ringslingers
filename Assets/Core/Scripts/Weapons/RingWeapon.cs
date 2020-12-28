[System.Serializable]
public struct RingWeapon
{
    /// <summary>
    /// The type of weapon this is an instance of
    /// </summary>
    public RingWeaponSettings weaponType;

    /// <summary>
    /// Current ammo or time remaining for this weapon
    /// </summary>
    public float ammo;

    /// <summary>
    /// Whether this weapon has infinite ammo
    /// </summary>
    public bool isInfinite;
}
