using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EquippedRingWeapon
{
    /// <summary>
    /// The type of weapon this is an instance of
    /// </summary>
    public RingWeaponSettings weaponType;

    /// <summary>
    /// Current ammo or time remaining for this weapon
    /// </summary>
    public float ammo;
}
