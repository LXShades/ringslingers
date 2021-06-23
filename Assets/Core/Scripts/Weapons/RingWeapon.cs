﻿using UnityEngine;

[System.Serializable]
public struct RingWeapon
{
    /// <summary>
    /// The type of weapon this is an instance of
    /// </summary>
    public RingWeaponSettingsAsset weaponType;

    /// <summary>
    /// Current ammo or time remaining for this weapon
    /// </summary>
    public float ammo
    {
        get => MatchState.Get(out MatchConfiguration config)
            && config.weaponAmmoStyle == WeaponAmmoStyle.Time ? Mathf.Max(_ammoAsTimeExpiryTime - GameTicker.singleton.predictedServerTime, 0) : _ammoAsCount;
        set
        {
            if (MatchState.Get(out MatchConfiguration config) && config.weaponAmmoStyle == WeaponAmmoStyle.Time)
                _ammoAsTimeExpiryTime = GameTicker.singleton.predictedServerTime + value;
            else
                _ammoAsCount = value;
        }
    }

    /// <summary>
    /// Whether this weapon has infinite ammo
    /// </summary>
    public bool isInfinite;



    /// <summary>
    /// only public for serialization; use ammo instead
    /// </summary>
    public float _ammoAsCount;
    /// <summary>
    /// only public for serialization; use ammo instead
    /// </summary>
    public float _ammoAsTimeExpiryTime;
}
