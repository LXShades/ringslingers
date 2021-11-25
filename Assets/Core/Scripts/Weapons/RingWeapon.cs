using System;

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
            && config.weaponAmmoStyle == WeaponAmmoStyle.Time ? (float)Math.Max(_ammoInternal - GameTicker.singleton.predictedServerTime, 0) : (float)_ammoInternal;
        set
        {
            if (MatchState.Get(out MatchConfiguration config) && config.weaponAmmoStyle == WeaponAmmoStyle.Time)
                _ammoInternal = GameTicker.singleton.predictedServerTime + value;
            else
                _ammoInternal = value;
        }
    }

    /// <summary>
    /// Whether this weapon has infinite ammo
    /// </summary>
    public bool isInfinite;

    /// <summary>
    /// only public for serialization; use ammo instead
    /// </summary>
    public double _ammoInternal;
}
