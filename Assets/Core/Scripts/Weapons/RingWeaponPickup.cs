using UnityEngine;

public class RingWeaponPickup : MonoBehaviour
{
    public RingWeaponSettings weaponType;

    public bool overrideAmmo = false;
    public float ammo = 0f;

    private void Awake()
    {
        if (GetComponent<Ring>())
        {
            GetComponent<Ring>().onPickup += OnRingPickedUp;
        }
    }

    private void OnRingPickedUp(Player player)
    {
        if (Mirror.NetworkServer.active)
            player.GetComponent<RingShooting>().AddWeaponAmmo(weaponType, overrideAmmo, ammo);
    }
}
