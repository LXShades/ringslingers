using Mirror;
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

    // SyncActions
    struct ThrowRingData : IMessageBase
    {
        public float time;
        public Vector3 position;
        public Vector3 direction;
        public uint ringNetId;
        public GameObject spawnedTemporaryRing;

        public void Serialize(NetworkWriter writer) { }
        public void Deserialize(NetworkReader reader) { }
    }
    private SyncAction<ThrowRingData> syncActionThrowRing;

    public override void WorldAwake()
    {
        player = GetComponent<Player>();
        syncActionThrowRing = SyncAction<ThrowRingData>.Register(gameObject, ConfirmRingThrow, PredictRingThrow, RewindRingThrow);
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

            syncActionThrowRing.Request(new ThrowRingData()
            {
                time = World.live.gameTime,
                position = spawnPosition.position,
                direction = player.input.aimDirection
                
            });
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

    private bool PredictRingThrow(ref ThrowRingData data)
    {
        // just call ConfirmRingThrow
        ConfirmRingThrow(ref data);

        return data.spawnedTemporaryRing != null;
    }

    private void RewindRingThrow(ref ThrowRingData data)
    {
        if (data.spawnedTemporaryRing)
        {
            World.Despawn(data.spawnedTemporaryRing);
        }

        lastFiredRingTime = 0;
    }

    private void ConfirmRingThrow(ref ThrowRingData data)
    {
        if (NetworkServer.active)
        {
            // prediction test
            data.position += Vector3.up;
        }

        if (data.time - lastFiredRingTime >= 1f / currentWeapon.weaponType.shotsPerSecond && player.numRings > 0)
        {
            GameObject ring = null;

            if (data.ringNetId > 0)
            {
                NetworkIdentity ringIdentity;

                Debug.Log($"Client received netId: {data.ringNetId}");

                if (NetworkIdentity.spawned.TryGetValue(data.ringNetId, out ringIdentity))
                {
                    ring = ringIdentity.gameObject;
                }
                else
                {
                    Debug.LogWarning($"Could not find confirmed ring ID of {data.ringNetId}");
                }
            }
            else
            {
                ring = World.Spawn(currentWeapon.weaponType.prefab, data.position, Quaternion.identity);
                data.spawnedTemporaryRing = ring;
            }

            if (ring != null)
            {
                ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();
                Debug.Assert(ringAsThrownRing);

                ringAsThrownRing.settings = currentWeapon.weaponType;
                ringAsThrownRing.Throw(player, data.position, data.direction);

                GameSounds.PlaySound(gameObject, currentWeapon.weaponType.fireSound);

                lastFiredRingTime = data.time;

                player.numRings--;
                if (!currentWeapon.weaponType.ammoIsTime)
                    currentWeapon.ammo--;

                hasFiredOnThisClick = true;

                if (NetworkServer.active)
                {
                    data.ringNetId = ring.GetComponent<NetworkIdentity>().netId;
                    Debug.Log($"Server adjusted netId to {data.ringNetId}");
                }
            }
        }
    }
}