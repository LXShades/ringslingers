using UnityEngine;

public class RingWeaponPickup : MonoBehaviour
{
    public RingWeaponSettingsAsset weaponType;

    public bool overrideAmmo = false;
    public float ammo = 0f;

    private void Awake()
    {
        if (TryGetComponent(out Ring ring))
        {
            ring.onPickup += OnRingPickedUp;
        }
    }

    private void OnRingPickedUp(Character player)
    {
        if (Mirror.NetworkServer.active)
            player.GetComponent<RingShooting>().AddWeaponAmmo(weaponType, overrideAmmo, ammo);
    }
}
