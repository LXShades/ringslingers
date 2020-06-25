using System.Collections;
using System.Collections.Generic;
using System.Security.Permissions;
using UnityEngine;

[CreateAssetMenu(fileName = "Ring Weapon", menuName = "Ring Weapon", order = 50)]
public class RingWeaponSettings : ScriptableObject
{
    /// <summary>
    /// Prefab used to create the ring weapon. This can define its behaviour, etc
    /// </summary>
    public GameObject prefab;

    /// <summary>
    /// Refire rate of this weapon ring
    /// </summary>
    public float shotsPerSecond = 3f;

    /// <summary>
    /// Sound effect to play when firing
    /// </summary>
    public GameSound fireSound;

    /// <summary>
    /// Maximum ammunition that can be held for this ring
    /// </summary>
    public int maxAmmo = 99;

    /// <summary>
    /// Maximum time that can be held for this weapon ring, in seconds
    /// </summary>
    public float maxTime = 99;

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
