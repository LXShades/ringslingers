using Mirror;
using System;
using System.Collections.Generic;
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

    /// <summary>
    /// [Server, Client] Generated from the combination of weapons. This is not networked directly but uses the weapons list which is.
    /// This handles combined weapon ring settings
    /// Currently updated whenever the weapons list refreshes
    /// </summary>
    public RingWeaponSettings effectiveWeaponSettings { get; private set; }

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

    private HistoryList<Action> bufferedThrowEvents = new HistoryList<Action>();

    public float testAutoAimSmoothDamp = 0.1f;
    private Vector3 autoAimDampVelocity;

    void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Start()
    {
        weapons.Callback += OnWeaponsListChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RegenerateEffectiveWeapon();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        weapons.Add(defaultWeapon);

        RegenerateEffectiveWeapon();
    }

    void Update()
    {
        UpdateAutoAim();

        for (int i = bufferedThrowEvents.Count - 1; i >= 0; i--)
        {
            if (Time.realtimeSinceStartup >= bufferedThrowEvents.TimeAt(i))
            {
                bufferedThrowEvents[i].Invoke();
                bufferedThrowEvents.RemoveAt(i);
            }
        }

        // Fire weapons if we can
        if (hasAuthority && player.input.btnFire && (!hasFiredOnThisClick || effectiveWeaponSettings.isAutomatic))
        {
            Debug.Assert(effectiveWeaponSettings.shotsPerSecond != 0); // division by zero otherwise

            LocalThrowRing();
        }

        if (NetworkServer.active)
        {
            // Remove weapons with no ammo remaining
            for (int i = 0; i < weapons.Count; i++)
            {
                if (!weapons[i].isInfinite && weapons[i].ammo <= 0)
                    weapons.RemoveAt(i--);
            }
        }

        hasFiredOnThisClick &= player.input.btnFire;
    }

    void UpdateAutoAim()
    {
        if (hasAuthority && false)
        {
            /*Player target = FindClosestTarget(10.0f);
            if (target)
            {
                if (PredictTargetPosition(target, out Vector3 predictedPosition))
                {
                    if (!testAutoAimObject.activeInHierarchy)
                    {
                        testAutoAimObject.transform.position = predictedPosition;
                        testAutoAimObject.transform.rotation = target.transform.rotation;
                        autoAimDampVelocity = Vector3.zero;
                        //testAutoAimObject.SetActive(true);
                    }
                    else
                    {
                        testAutoAimObject.transform.position = Vector3.SmoothDamp(testAutoAimObject.transform.position, predictedPosition, ref autoAimDampVelocity, testAutoAimSmoothDamp);
                    }
                }
                else
                    testAutoAimObject.SetActive(false);
            }
            else
                testAutoAimObject.SetActive(false);*/
        }
    }

    private Player FindClosestTarget(float angleLimit)
    {
        Vector3 aimDirection = player.input.aimDirection;
        float bestDot = Mathf.Cos(angleLimit * Mathf.Deg2Rad);
        Player bestTarget = null;

        foreach (Player player in Netplay.singleton.players)
        {
            if (player && player != this.player)
            {
                float dot = Vector3.Dot((player.transform.position - transform.position).normalized, aimDirection);

                if (dot >= bestDot)
                {
                    bestDot = dot;
                    bestTarget = player;
                }
            }
        }

        return bestTarget;
    }

    private bool PredictTargetPosition(Player target, out Vector3 predictedPosition)
    {
        float interval = 0.07f;
        PlayerController controller = target.GetComponent<PlayerController>();
        CharacterMovement movement = target.GetComponent<CharacterMovement>();
        PlayerController.MoveState originalState = controller.MakeMoveState();
        PlayerInput input = controller.GetLatestInput();
        Vector3 startPosition = spawnPosition.position;
        float ringDistance = 0f; // theoretical thrown ring distance
        float ringSpeed = 32.81f * interval;
        float lastTargetDistance = Vector3.Distance(target.transform.position, startPosition);
        Vector3 lastTargetPosition = target.transform.position;
        bool succeeded = false;

        predictedPosition = target.transform.position;

        for (int i = 0; i < 30; i++)
        {
            movement.TickMovement(interval, input, true);
            ringDistance += ringSpeed;

            float currentTargetDistance = Vector3.Distance(target.transform.position, startPosition);

            if (currentTargetDistance >= lastTargetDistance + ringSpeed) // target is moving away faster than our ring would, so if they continue along this path we can't hit them
                break;

            if (ringDistance >= currentTargetDistance)
            {
                // the ring is normally a bit ahead, estimate a position between the last and current position using the typical gap per interval as a point of reference
                float blend = 1f - (ringDistance - currentTargetDistance) / ringSpeed;
                predictedPosition = Vector3.LerpUnclamped(lastTargetPosition, target.transform.position, blend);
                succeeded = true;
                break;
            }

            lastTargetDistance = currentTargetDistance;
            lastTargetPosition = target.transform.position;
        }

        controller.ApplyMoveState(originalState);
        return succeeded;
    }

    public void AddWeaponAmmo(RingWeaponSettingsAsset weaponType, bool doOverrideAmmo, float ammoOverride)
    {
        float ammoToAdd = doOverrideAmmo ? ammoOverride : weaponType.settings.ammoOnPickup;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponType == weaponType)
            {
                RingWeapon weapon = weapons[i];

                weapon.ammo = Mathf.Min(weapon.ammo + ammoToAdd, weaponType.settings.maxAmmo);
                weapons[i] = weapon;
                return;
            }
        }

        // no weapon was found - add to our list
        weapons.Add(new RingWeapon() { weaponType = weaponType, ammo = ammoToAdd });
    }

    private bool CanThrowRing(float lenience) => player.numRings > 0 && Time.time - lastFiredRingTime >= 1f / effectiveWeaponSettings.shotsPerSecond - lenience;

    private void LocalThrowRing()
    {
        if (CanThrowRing(0f))
        {
            if (!NetworkServer.active)
            {
                // spawn temporary ring
                Spawner.StartSpawnPrediction();
                GameObject predictedRing = Spawner.PredictSpawn(effectiveWeaponSettings.prefab, transform.position, Quaternion.identity);
                FireSpawnedRing(predictedRing, spawnPosition.position, player.input.aimDirection);
            }

            Vector3 direction = player.input.aimDirection;
            /*Player autoAimTarget = FindClosestTarget(10.0f);

            if (autoAimTarget)
            {
                if (PredictTargetPosition(autoAimTarget, out Vector3 predictedPosition))
                    direction = predictedPosition + Vector3.up * 0.5f - spawnPosition.position;
            }*/

            CmdThrowRing(spawnPosition.position, direction, Spawner.EndSpawnPrediction(), equippedWeaponIndex, PlayerTicker.singleton ? PlayerTicker.singleton.predictedServerTime : 0f);
            hasFiredOnThisClick = true;
        }
    }

    [Command]
    private void CmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, int equippedWeapon, float predictedServerTime)
    {
        float timeToThrowAt = predictedServerTime;
        if (timeToThrowAt > Time.realtimeSinceStartup)
        {
            // the client threw this, in what they predicted ahead of current time... this means we need to delay the shot until roughly the correct time arrives
             bufferedThrowEvents.Insert(timeToThrowAt, () => OnCmdThrowRing(position, direction, spawnPrediction, equippedWeapon, predictedServerTime));
        }
        else
        {
            // throw it now then
            OnCmdThrowRing(position, direction, spawnPrediction, equippedWeapon, predictedServerTime);
        }
    }

    private void OnCmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, int equippedWeapon, float predictedServerTime)
    {
        if (!CanThrowRing(0.01f) || Vector3.Distance(position, spawnPosition.position) > 2f)
        {
            Log.WriteWarning($"Discarding ring throw: dist is {Vector3.Distance(position, spawnPosition.position)} CanThrow: {CanThrowRing(0.01f)}");
            return; // invalid throw
        }

        equippedWeaponIndex = equippedWeapon;

        // on server, spawn the ring properly and match it to the client prediction
        Spawner.ApplySpawnPrediction(spawnPrediction);
        GameObject ring = Spawner.StartSpawn(effectiveWeaponSettings.prefab, position, Quaternion.identity);

        if (ring != null)
            FireSpawnedRing(ring, position, direction);

        Spawner.FinalizeSpawn(ring);

        // Update stats
        player.numRings--;
        if (!effectiveWeaponSettings.ammoIsTime)
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

        ringAsThrownRing.Throw(player, position, direction);

        GameSounds.PlaySound(gameObject, effectiveWeaponSettings.fireSound);

        lastFiredRingTime = Time.time;
    }

    private void OnWeaponsListChanged(SyncList<RingWeapon>.Operation op, int itemIndex, RingWeapon oldItem, RingWeapon newItem)
    {
        RegenerateEffectiveWeapon();
    }

    private void RegenerateEffectiveWeapon()
    {
        // figure out which weapon takes priority. some examples as of whenever this comment was written (who am i. what day is it)
        // Bomb ring effects:
        //   ^-- Automatic ring
        // Rail ring effects
        //   ^-- Automatic ring
        //   ^-- Bomb ring
        // Automatic ring effects
        //   ^-- Aim maybe
        // basically, anything that is included as an effector by another weapon is excluded from consideration as the main weapon
        List<RingWeaponSettingsAsset> primaries = new List<RingWeaponSettingsAsset>();
        List<RingWeaponSettingsAsset> effectors = new List<RingWeaponSettingsAsset>();

        foreach (var weapon in weapons)
            primaries.Add(weapon.weaponType);

        // determine the primary weapon to use
        for (int current = 0; current < primaries.Count; current++)
        {
            bool shouldExcludeCurrent = false;
            for (int other = 0; other < primaries.Count; other++)
            {
                if (other != current)
                {
                    foreach (var comboSettings in primaries[other].settings.comboSettings)
                    {
                        if (comboSettings.effector == primaries[current])
                            shouldExcludeCurrent = true;
                    }
                }
            }

            if (shouldExcludeCurrent)
            {
                // move from primary to effector
                effectors.Add(primaries[current]);
                primaries.RemoveAt(current--);
            }

            equippedWeaponIndex = 0;
        }

        if (primaries.Count > 0)
        {
            // select the second weapon (first is always the default red ring) as the primary
            equippedWeaponIndex = Mathf.Min(1, primaries.Count - 1);

            effectiveWeaponSettings = primaries[equippedWeaponIndex].settings.Clone();

            // apply combos
            string effectsDebug = "";
            foreach (var combo in effectiveWeaponSettings.comboSettings)
            {
                if (effectors.Contains(combo.effector))
                {
                    combo.ApplyToSettings(effectiveWeaponSettings);
                    effectsDebug += $"{combo.effector}";
                }
            }

            Log.Write($"Primary weapon is {equippedWeaponIndex}, effects are {effectsDebug}");
        }
        else
            Log.WriteWarning("Cannot generate primary weapon: there is none.");
    }
}