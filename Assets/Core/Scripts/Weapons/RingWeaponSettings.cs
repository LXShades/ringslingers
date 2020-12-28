using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "Ring Weapon", menuName = "Ring Weapon", order = 50)]
public class RingWeaponSettings : ScriptableObject, ILookupableAsset
{
    /// <summary>
    /// Prefab used to create the ring weapon. This can define its behaviour, etc
    /// </summary>
    public GameObject prefab;

    /// <summary>
    /// Image representing the weapon in the in-game HUD
    /// </summary>
    public Sprite uiIcon;

    /// <summary>
    /// Refire rate of this weapon ring
    /// </summary>
    public float shotsPerSecond = 3f;

    /// <summary>
    /// Sound effect to play when firing
    /// </summary>
    public GameSound fireSound;

    /// <summary>
    /// Sound effect to play when hitting a wall or player and despawning
    /// </summary>
    public GameSound despawnSound;

    /// <summary>
    /// How much ammunition should be granted when this weapon is picked up
    /// </summary>
    public float ammoOnPickup = 10;

    /// <summary>
    /// Maximum ammunition that can be held for this weapon ring
    /// </summary>
    public float maxAmmo = 99;

    /// <summary>
    /// If true, ammo is a timer that counts down while the weapon is held
    /// </summary>
    public bool ammoIsTime = true;

    /// <summary>
    /// Speed of the ring projectile
    /// </summary>
    public float projectileSpeed = 60;

    /// <summary>
    /// The speed that the ring projectile spins at, in degrees/sec
    /// </summary>
    public float projectileSpinSpeed = 270;

    /// <summary>
    /// How long the projectile will last before destructing, in seconds
    /// </summary>
    public float projectileLifetime = 10;

    /// <summary>
    /// Whether the shoot button can be held down with this weapon
    /// </summary>
    public bool isAutomatic = false;
}

public static class RingWeaponSettingsReaderWriter
{
    public static RingWeaponSettings ReadRingWeaponSettings(this NetworkReader reader)
    {
        ushort id = reader.ReadUInt16();

        return AssetLookup.singleton.GetAsset<RingWeaponSettings>(id);
    }

    public static void WriteRingWeaponSettings(this NetworkWriter writer, RingWeaponSettings settings)
    {
        writer.WriteUInt16(AssetLookup.singleton.GetId(settings));
    }
}