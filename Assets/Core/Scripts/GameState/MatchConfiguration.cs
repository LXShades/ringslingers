using Mirror;
using UnityEngine;

public enum WeaponAmmoStyle : byte
{
    Time,
    Quantity
}

public enum WeaponCombinationStyle : byte
{
    Combinable,
    Separate
}

public class MatchConfiguration : MatchStateComponent
{
    [Header("Weapon settings")]
    [SyncVar]
    public WeaponAmmoStyle weaponAmmoStyle = WeaponAmmoStyle.Time;
    [SyncVar]
    public WeaponCombinationStyle weaponCombinationStyle = WeaponCombinationStyle.Combinable;

    public override void OnStart()
    {
        MapConfiguration config = GameState.Get(out GameState_Map gsMap) ? gsMap.activeMap : null;

        if (config != null)
        {
            weaponAmmoStyle = config.defaultWeaponAmmoStyle;
            weaponCombinationStyle = config.defaultWeaponCombinationStyle;
        }
    }

    public override void OnUpdate()
    {
    }
}
