using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class RingShooting : NetworkBehaviour
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

    void Awake()
    {
        player = GetComponent<Player>();
    }

    void Update()
    {
        if (equippedWeapons.Count > 0)
            currentWeapon = equippedWeapons[equippedWeapons.Count - 1];
        else
            currentWeapon = defaultWeapon;

        // Fire weapons if we can
        if (player.input.btnFire && (!hasFiredOnThisClick || currentWeapon.weaponType.isAutomatic))
        {
            Debug.Assert(currentWeapon.weaponType.shotsPerSecond != 0); // division by zero otherwise

            LocalThrowRing();
            hasFiredOnThisClick = true;
        }

        // Deplete timer-based weapon ammo
        for (int i = 0; i < equippedWeapons.Count; i++)
        {
            if (equippedWeapons[i].weaponType.ammoIsTime)
                equippedWeapons[i].ammo -= Time.deltaTime;
        }

        // Remove weapons with no ammo remaining
        for (int i = 0; i < equippedWeapons.Count; i++)
        {
            if (equippedWeapons[i].ammo <= 0)
                equippedWeapons.RemoveAt(i--);
        }

        hasFiredOnThisClick &= player.input.btnFire;
    }

    public void AddWeaponAmmo(RingWeaponSettings weaponType)
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

    private bool CanThrowRing() => player.numRings > 0 && Time.time - lastFiredRingTime >= 1f / currentWeapon.weaponType.shotsPerSecond;

    private void LocalThrowRing()
    {
        if (CanThrowRing())
        {
            // spawn temporary ring
            Spawner.StartSpawnPrediction();
            GameObject predictedRing = Spawner.PredictSpawn(currentWeapon.weaponType.prefab, transform.position, Quaternion.identity);
            FireSpawnedRing(predictedRing, spawnPosition.position, player.input.aimDirection);

            CmdThrowRing(spawnPosition.position, player.input.aimDirection, Spawner.EndSpawnPrediction());
        }
    }

    [Command]
    private void CmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction)
    {
        if (!CanThrowRing() || Vector3.Distance(position, spawnPosition.position) > 0.5f || Mathf.Abs(direction.sqrMagnitude - 1.0f) > 0.01f)
            return; // invalid throw

        // on server, spawn the ring properly and match it to the client prediction
        Spawner.ApplySpawnPrediction(spawnPrediction);
        GameObject ring = Spawner.StartSpawn(currentWeapon.weaponType.prefab, position, Quaternion.identity);

        if (ring != null)
            FireSpawnedRing(ring, position, direction);

        Spawner.FinalizeSpawn(ring);

        // RPC it to other clients
        // wait, actually it'll already be spawned
        //RpcThrowRing(ring, position, direction);

        // tell the client this was successful
        TargetThrowRing();

        // Update stats
        player.numRings--;
        if (!currentWeapon.weaponType.ammoIsTime)
            currentWeapon.ammo--;
    }

    [TargetRpc]
    private void TargetThrowRing()
    {

    }

    private void FireSpawnedRing(GameObject ring, Vector3 position, Vector3 direction)
    {
        ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();
        Debug.Assert(ringAsThrownRing);

        ringAsThrownRing.settings = currentWeapon.weaponType;
        ringAsThrownRing.Throw(player, position, direction, 0);

        GameSounds.PlaySound(gameObject, currentWeapon.weaponType.fireSound);

        lastFiredRingTime = Time.time;
    }
}