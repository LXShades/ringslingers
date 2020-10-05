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
        public WorldObject spawnedTemporaryRing;

        public void Serialize(NetworkWriter writer) { }
        public void Deserialize(NetworkReader reader) { }
    }
    private SyncAction<ThrowRingData> syncActionThrowRing;

    public override void WorldAwake()
    {
        player = GetComponent<Player>();
    }

    public override void WorldStart()
    {
        syncActionThrowRing = new SyncAction<ThrowRingData>(gameObject, ConfirmRingThrow, PredictRingThrow, RewindRingThrow); // here in Start because netIds aren't known at the Awake stage, unfortunately, meaning SyncAction binding needs to happen later
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

    private bool PredictRingThrow(SyncActionChain chain, ref ThrowRingData data)
    {
        // just call ConfirmRingThrow
        return ConfirmRingThrow(chain, ref data);
    }

    private void RewindRingThrow(SyncActionChain chain, ref ThrowRingData data)
    {
        if (data.spawnedTemporaryRing)
        {
            World.Despawn(data.spawnedTemporaryRing.gameObject);
            player.numRings++;
        }

        lastFiredRingTime = 0;
    }

    private bool ConfirmRingThrow(SyncActionChain chain, ref ThrowRingData data)
    {
        if (data.time - lastFiredRingTime >= 1f / currentWeapon.weaponType.shotsPerSecond && player.numRings > 0)
        {
            /*GameObject ring = null;

            if (data.ringNetId > 0)
            {
                NetworkIdentity ringIdentity;

                if (NetworkIdentity.spawned.TryGetValue(data.ringNetId, out ringIdentity))
                {
                    ring = ringIdentity.gameObject;
                }
                else
                {
                    Log.WriteWarning($"Could not find confirmed ring ID of {data.ringNetId}");
                }
            }
            else
            {
                ring = World.Spawn(currentWeapon.weaponType.prefab, data.position, Quaternion.identity);
                data.spawnedTemporaryRing = ring.GetComponent<WorldObject>();
            }

            if (ring != null)
            {
                ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();
                Debug.Assert(ringAsThrownRing);

                ringAsThrownRing.settings = currentWeapon.weaponType;
                ringAsThrownRing.Throw(player, data.position, data.direction, chain.timeSinceRequest);

                GameSounds.PlaySound(gameObject, currentWeapon.weaponType.fireSound);

                lastFiredRingTime = data.time;

                player.numRings--;
                if (!currentWeapon.weaponType.ammoIsTime)
                    currentWeapon.ammo--;

                hasFiredOnThisClick = true;

                if (NetworkServer.active)
                {
                    data.ringNetId = ring.GetComponent<NetworkIdentity>().netId;
                }
            }*/
            Log.Write("Ring throw! Spawning two objects to see what happens lolol");
            World.live.syncActionSpawnObject.Request(new World.SpawnObjectData() { prefab = currentWeapon.weaponType.prefab, position = data.position });
            World.live.syncActionSpawnObject.Request(new World.SpawnObjectData() { prefab = currentWeapon.weaponType.prefab, position = data.position + Vector3.up });

            return true;
        }
        else
        {
            return false; // nowt throwin'
        }
    }
}