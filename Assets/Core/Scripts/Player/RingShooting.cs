using Mirror;
using UnityEngine;

public class RingShooting : NetworkBehaviour
{
    /// <summary>
    /// The default weapon setup
    /// </summary>
    public RingWeapon defaultWeapon;

    /// <summary>
    /// The weapon currently equipped to fire
    /// </summary>
    public RingWeapon currentWeapon => weapons[equippedWeaponIndex];

    /// <summary>
    /// List of weapons that have been picked up
    /// </summary>
    public SyncList<RingWeapon> weapons = new SyncList<RingWeapon>();

    [Header("Hierarchy")]
    /// <summary>
    /// Where to spawn the ring from
    /// </summary>
    public Transform spawnPosition;

    /// <summary>
    /// When the last ring was fired
    /// </summary>
    private float lastFiredRingTime = -1;

    /// <summary>
    /// Er, it's hard to explain. Although useless, this comment loves you.
    /// </summary>
    private bool hasFiredOnThisClick = false;

    private int equippedWeaponIndex
    {
        set
        {
            _equippedWeaponIndex = value < weapons.Count ? value : 0;
        }
        get
        {
            return _equippedWeaponIndex;
        }
    }
    private int _equippedWeaponIndex;

    // Components
    private Player player;

    void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Start()
    {
        if (NetworkServer.active)
        {
            weapons.Add(defaultWeapon);
        }
    }

    void Update()
    {
        // test: equip best weapon
        if (weapons.Count > 0)
            equippedWeaponIndex = weapons.Count - 1;

        // Fire weapons if we can
        if (hasAuthority && player.input.btnFire && (!hasFiredOnThisClick || currentWeapon.weaponType.isAutomatic))
        {
            Debug.Assert(currentWeapon.weaponType.shotsPerSecond != 0); // division by zero otherwise

            LocalThrowRing();
        }

        if (NetworkServer.active)
        {
            // Deplete timer-based weapon ammo
            for (int i = 0; i < weapons.Count; i++)
            {
                if (!weapons[i].isInfinite && weapons[i].weaponType.ammoIsTime)
                {
                    RingWeapon weapon = weapons[i];
                    weapon.ammo -= Time.deltaTime;
                    weapons[i] = weapon;
                }
            }

            // Remove weapons with no ammo remaining
            for (int i = 0; i < weapons.Count; i++)
            {
                if (!weapons[i].isInfinite && weapons[i].ammo <= 0)
                    weapons.RemoveAt(i--);
            }
        }

        hasFiredOnThisClick &= player.input.btnFire;
    }

    public void AddWeaponAmmo(RingWeaponSettings weaponType, bool doOverrideAmmo, float ammoOverride)
    {
        float ammoToAdd = doOverrideAmmo ? ammoOverride : weaponType.ammoOnPickup;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponType == weaponType)
            {
                RingWeapon weapon = weapons[i];

                weapon.ammo = Mathf.Min(weapon.ammo + ammoToAdd, weaponType.maxAmmo);
                weapons[i] = weapon;
                return;
            }
        }

        // no weapon was found - add to our list
        weapons.Add(new RingWeapon() { weaponType = weaponType, ammo = ammoToAdd });
    }

    private bool CanThrowRing() => player.numRings > 0 && Time.time - lastFiredRingTime >= 1f / currentWeapon.weaponType.shotsPerSecond;

    private void LocalThrowRing()
    {
        if (CanThrowRing())
        {
            if (!NetworkServer.active)
            {
                // spawn temporary ring
                Spawner.StartSpawnPrediction();
                GameObject predictedRing = Spawner.PredictSpawn(currentWeapon.weaponType.prefab, transform.position, Quaternion.identity);
                FireSpawnedRing(predictedRing, spawnPosition.position, player.input.aimDirection);
            }

            CmdThrowRing(spawnPosition.position, player.input.aimDirection, Spawner.EndSpawnPrediction(), equippedWeaponIndex);
            hasFiredOnThisClick = true;
        }
    }

    [Command]
    private void CmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, int equippedWeapon)
    {
        if (!CanThrowRing() || Vector3.Distance(position, spawnPosition.position) > 2f)
        {
            Log.WriteWarning($"Discarding ring throw: dist is {Vector3.Distance(position, spawnPosition.position)} CanThrow: {CanThrowRing()}");
            return; // invalid throw
        }

        equippedWeaponIndex = equippedWeapon;

        // on server, spawn the ring properly and match it to the client prediction
        Spawner.ApplySpawnPrediction(spawnPrediction);
        GameObject ring = Spawner.StartSpawn(currentWeapon.weaponType.prefab, position, Quaternion.identity);

        if (ring != null)
            FireSpawnedRing(ring, position, direction);

        Spawner.FinalizeSpawn(ring);

        // Update stats
        player.numRings--;
        if (!currentWeapon.weaponType.ammoIsTime)
        {
            RingWeapon weapon = weapons[equippedWeaponIndex];
            weapon.ammo--;
            weapons[equippedWeaponIndex] = weapon;
        }
    }

    private void FireSpawnedRing(GameObject ring, Vector3 position, Vector3 direction)
    {
        ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();
        Debug.Assert(ringAsThrownRing);

        ringAsThrownRing.settings = currentWeapon.weaponType;
        ringAsThrownRing.Throw(player, position, direction);

        GameSounds.PlaySound(gameObject, currentWeapon.weaponType.fireSound);

        lastFiredRingTime = Time.time;
    }
}