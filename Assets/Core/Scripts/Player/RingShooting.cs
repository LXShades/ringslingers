using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RingShooting : SyncedObject
{
    /// <summary>
    /// The default weapon to fire
    /// </summary>
    public RingWeaponSettings currentWeapon;

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

    // Components
    private Player player;

    public override void FrameAwake()
    {
        player = GetComponent<Player>();
    }

    public override void FrameUpdate()
    {
        if (player.input.btnFire && (!player.lastInput.btnFire || currentWeapon.isAutomatic))
        {
            Debug.Assert(currentWeapon.shotsPerSecond != 0); // division by zero otherwise
            
            // Fire if we can
            if (Frame.local.time - lastFiredRingTime >= 1f / currentWeapon.shotsPerSecond)
            {
                GameObject ring = Instantiate(currentWeapon.prefab, spawnPosition.position, Quaternion.identity);
                ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();

                Debug.Assert(ringAsThrownRing);
                ringAsThrownRing.settings = currentWeapon;
                ringAsThrownRing.Throw(player, spawnPosition.position, player.aimForward);

                GameSounds.PlaySound(gameObject, currentWeapon.fireSound);

                lastFiredRingTime = Frame.local.time;
            }
        }
    }
}
