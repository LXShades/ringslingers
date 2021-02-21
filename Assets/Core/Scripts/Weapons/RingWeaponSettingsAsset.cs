﻿using Mirror;
using UnityEngine;

[CreateAssetMenu(fileName = "Ring Weapon", menuName = "Ring Weapon", order = 50)]
public class RingWeaponSettingsAsset : ScriptableObject, ILookupableAsset
{
    public RingWeaponSettings settings;
}

[System.Serializable]
public class RingWeaponSettings
{
    public enum SpinAxisType
    {
        Up,
        Forward,
        Wobble
    }

    public RingWeaponSettings() { }

    public RingWeaponSettings Clone()
    {
        return (RingWeaponSettings)MemberwiseClone();
    }

    [Header("Setup")]
    /// <summary>
    /// Prefab used to create the ring weapon. This can define its behaviour, etc
    /// </summary>
    public GameObject prefab;

    /// <summary>
    /// Prefab used to drop the weapon when hit
    /// </summary>
    public GameObject droppedRingPrefab;

    /// <summary>
    /// Image representing the weapon in the in-game HUD
    /// </summary>
    public Sprite uiIcon;

    [Header("Sound effects")]
    /// <summary>
    /// Sound effect to play when firing
    /// </summary>
    public GameSound fireSound;

    /// <summary>
    /// Sound effect to play when hitting a wall or player and despawning
    /// </summary>
    public GameSound despawnSound;

    [Header("Stats")]
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

    [Header("Firing")]
    /// <summary>
    /// Refire rate of this weapon ring
    /// </summary>
    public float shotsPerSecond = 3f;

    /// <summary>
    /// Whether the shoot button can be held down with this weapon
    /// </summary>
    public bool isAutomatic = false;

    /// <summary>
    /// Degrees of autoaim something I... i dont know what im saying
    /// </summary>
    public float autoAimHitboxRadius = 0f;

    public float autoAimDegreesRadius = 0f;

    [Header("Projectile")]
    /// <summary>
    /// Speed of the ring projectile
    /// </summary>
    public float projectileSpeed = 60;

    /// <summary>
    /// How the projectile spins
    /// </summary>
    public SpinAxisType projectileSpinAxisType = SpinAxisType.Up;

    /// <summary>
    /// The speed that the ring projectile spins at, in degrees/sec
    /// </summary>
    public float projectileSpinSpeed = 270;

    /// <summary>
    /// How long the projectile will last before destructing, in seconds
    /// </summary>
    public float projectileLifetime = 10;

    [Header("Contact")]
    /// <summary>
    /// Spawns an optional effect on collide and despawn
    /// </summary>
    public GameObject contactEffect = null;

    /// <summary>
    /// How fast in m/s will the projectile knock you back?
    /// </summary>
    public float projectileKnockback = 10f;

    [Header("Ring Combinations")]
    /// <summary>
    /// A list of weapon rings and the effects they have on this ring when combined
    /// </summary>
    public RingWeaponComboSettings[] comboSettings = new RingWeaponComboSettings[0];
}

[System.Serializable]
public class RingWeaponComboSettings
{
    [Header("Target")]
    [Tooltip("The other weapon that effects this weapon")]
    public RingWeaponSettingsAsset effector = null;

    [Header("Modifiers")]
    [Tooltip("How much to multiple the refire rate of this weapon when holding the effector weapon")]
    public float shotsPerSecondMultiplier = 1;

    [Tooltip("Whether to override automaticness on this weapon when holding the effector")]
    public bool shouldOverrideAutomatic = false;
    public bool isAutomatic = false;

    public bool shouldOverrideDespawnSound = false;
    public GameSound despawnSound = new GameSound();

    [Tooltip("A contact effect to use when holding the effector weapon")]
    public GameObject contactEffectOverride = null;
    // prefaboverride? if we really wanted to do a new model per weapon combination which, for now, and maybe forever, nah.

    public void ApplyToSettings(RingWeaponSettings settings)
    {
        settings.shotsPerSecond *= shotsPerSecondMultiplier;

        if (shouldOverrideAutomatic)
            settings.isAutomatic = isAutomatic;

        if (contactEffectOverride)
            settings.contactEffect = contactEffectOverride;

        if (shouldOverrideDespawnSound)
            settings.despawnSound = despawnSound;
    }
}

public struct Modifier<TValue>
{
    public enum Type
    {
        Ignore = 0,
        Override,
        Add,
        Multiply
    }

    public TValue value;
    public Type type;
}

public static class RingWeaponSettingsReaderWriter
{
    public static RingWeaponSettingsAsset ReadRingWeaponSettings(this NetworkReader reader)
    {
        ushort id = reader.ReadUInt16();

        return AssetLookup.singleton.GetAsset<RingWeaponSettingsAsset>(id);
    }

    public static void WriteRingWeaponSettings(this NetworkWriter writer, RingWeaponSettingsAsset settings)
    {
        writer.WriteUInt16(AssetLookup.singleton.GetId(settings));
    }
}