using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable, WorldClonable]
public class EquippedRingWeapon
{
    /// <summary>
    /// The type of weapon this is an instance of
    /// </summary>
    [WorldSharedReference] public RingWeaponSettings weaponType;

    /// <summary>
    /// Current ammo or time remaining for this weapon
    /// </summary>
    public float ammo;
}
