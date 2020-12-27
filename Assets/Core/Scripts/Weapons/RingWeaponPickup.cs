using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RingWeaponPickup : MonoBehaviour
{
    public RingWeaponSettings weaponType;

    private void Awake()
    {
        if (GetComponent<Ring>())
        {
            GetComponent<Ring>().onPickup += OnRingPickedUp;
        }
    }

    private void OnRingPickedUp(Player player)
    {
        player.GetComponent<RingShooting>().AddWeaponAmmo(weaponType);
    }
}
