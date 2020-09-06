using System.Collections.Generic;
using UnityEngine;

public class RingShooting : WorldObjectComponent
{
    /// <summary>
    /// The default weapon to fire
    /// </summary>
    public EquippedRingWeapon defaultWeapon;

    /// <summary>
    /// The weapon currently equipped to fire
    /// </summary>
    public EquippedRingWeapon currentWeapon;

    /// <summary>
    /// List of weapons that have been picked up
    /// </summary>
    public List<EquippedRingWeapon> equippedWeapons = new List<EquippedRingWeapon>();

    [Header("Hierarchy")]
    /// <summary>
    /// Where to spawn the ring from
    /// </summary>
    public Transform spawnPosition;

    /// <summary>
    /// Nudges the ring forward by this amount upon initial spawn
    /// </summary>
    public float spawnNudge = 0.469f;

    /// <summary>
    /// When the last ring was fired
    /// </summary>
    private float lastFiredRingTime = -1;

    /// <summary>
    /// Er, it's hard to explain. Although useless, this comment loves you.
    /// </summary>
    private bool hasFiredOnThisClick = false;

    // Components
    private Player player;

    public override void WorldAwake()
    {
        player = GetComponent<Player>();
    }

    public override void WorldUpdate(float deltaTime)
    {
        if (equippedWeapons.Count > 0)
            currentWeapon = equippedWeapons[equippedWeapons.Count - 1];
        else
            currentWeapon = defaultWeapon;

        // Fire weapons if we can
        if (player.input.btnFire && (!hasFiredOnThisClick || currentWeapon.weaponType.isAutomatic))
        {
            Debug.Assert(currentWeapon.weaponType.shotsPerSecond != 0); // division by zero otherwise
            
            if (World.live.gameTime - lastFiredRingTime >= 1f / currentWeapon.weaponType.shotsPerSecond && player.numRings > 0)
            {
                GameObject ring = World.Spawn(currentWeapon.weaponType.prefab, spawnPosition.position, Quaternion.identity);
                ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();

                Debug.Assert(ringAsThrownRing);
                ringAsThrownRing.settings = currentWeapon.weaponType;
                ringAsThrownRing.Throw(player, spawnPosition.position, player.input.aimDirection);

                GameSounds.PlaySound(gameObject, currentWeapon.weaponType.fireSound);

                lastFiredRingTime = World.live.gameTime;

                player.numRings--;
                if (!currentWeapon.weaponType.ammoIsTime)
                    currentWeapon.ammo--;

                hasFiredOnThisClick = true;
            }
        }

        // Deplete timer-based weapon ammo
        for (int i = 0; i < equippedWeapons.Count; i++)
        {
            if (equippedWeapons[i].weaponType.ammoIsTime)
                equippedWeapons[i].ammo -= deltaTime;
        }

        // Remove weapons with no ammo remaining
        for (int i = 0; i < equippedWeapons.Count; i++)
        {
            if (equippedWeapons[i].ammo <= 0)
                equippedWeapons.RemoveAt(i--);
        }
   
        hasFiredOnThisClick &= player.input.btnFire;
    }

    public void AddWeapon(RingWeaponSettings weaponType)
    {
        foreach (EquippedRingWeapon weapon in equippedWeapons)
        {
            if (weapon.weaponType == weaponType)
            {
                weapon.ammo = Mathf.Min(weapon.ammo + weaponType.ammoOnPickup, weaponType.maxAmmo);
                return;
            }
        }

        // no weapon was found - add to our list
        equippedWeapons.Add(new EquippedRingWeapon() { weaponType = weaponType, ammo = weaponType.ammoOnPickup });
    }
}
