using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterShooting : NetworkBehaviour
{
    /// <summary>
    /// The default weapon setup
    /// </summary>
    public RingWeapon defaultWeapon;

    /// <summary>
    /// List of weapons that have been picked up
    /// </summary>
    public SyncList<RingWeapon> weapons = new SyncList<RingWeapon>();

    /// <summary>
    /// When applicable, this is the weapon that has been selected by this player. Typically only used for local players
    /// </summary>
    public RingWeaponSettingsAsset[] localSelectedWeapons
    {
        get => _localSelectedWeapons;
        set
        {
            // don't bother changing if lists are identical
            bool isListIdentical = false;

            if (value.Length == _localSelectedWeapons.Length)
            {
                isListIdentical = true;
                for (int i = 0; i < _localSelectedWeapons.Length; i++)
                {
                    if (_localSelectedWeapons[i] != value[i])
                    {
                        isListIdentical = false;
                        break;
                    }
                }
            }

            if (!isListIdentical)
            {
                _localSelectedWeapons = value;

                if (hasAuthority && !isServer)
                    CmdSelectWeapon(_localSelectedWeapons); // we don't use a syncvar because not all players need to know about it
                RegenerateEffectiveWeapon();
            }
        }
    }
    private RingWeaponSettingsAsset[] _localSelectedWeapons = new RingWeaponSettingsAsset[0];

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

    public GameObject autoAimTarget { get;  private set; }
    private Vector3 autoAimPredictedDirection = Vector3.zero;

    // Components
    private Character player;
    private CharacterMovement movement;

    private TimelineList<Action> bufferedThrowEvents = new TimelineList<Action>();

    public float testAutoAimSmoothDamp = 0.1f;
    private Vector3 autoAimDampVelocity;

    void Awake()
    {
        player = GetComponent<Character>();
        movement = GetComponent<CharacterMovement>();
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
        if (hasAuthority && player.liveInput.btnFire && (!hasFiredOnThisClick || effectiveWeaponSettings.isAutomatic))
        {
            Debug.Assert(effectiveWeaponSettings.shotsPerSecond != 0); // division by zero otherwise

            LocalThrowRing();
        }

        if (NetworkServer.active)
        {
            // Remove weapons with no ammo remaining
            bool shouldRegenerateWeapon = false;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (!weapons[i].isInfinite && weapons[i].ammo <= 0)
                {
                    weapons.RemoveAt(i--);
                    shouldRegenerateWeapon = true;
                }
            }

            if (shouldRegenerateWeapon)
                RegenerateEffectiveWeapon();
        }

        hasFiredOnThisClick &= player.liveInput.btnFire;
    }

    void UpdateAutoAim()
    {
        autoAimTarget = null;

        if (hasAuthority && effectiveWeaponSettings.autoAimHitboxRadius > 0f && effectiveWeaponSettings.autoAimDegreesRadius > 0f)
        {
            Character potentialAutoAimTarget = FindClosestTarget(effectiveWeaponSettings.autoAimDegreesRadius);

            if (potentialAutoAimTarget)
            {
                Vector3 targetPosAdjusted = potentialAutoAimTarget.transform.position + Vector3.up * 0.5f;
                Vector3 nearTargetPoint = spawnPosition.position + player.liveInput.aimDirection * Vector3.Dot(player.liveInput.aimDirection, targetPosAdjusted - spawnPosition.position);

                if (Vector3.Distance(nearTargetPoint, targetPosAdjusted) <= effectiveWeaponSettings.autoAimHitboxRadius)
                {
                    if (PredictTargetPosition(potentialAutoAimTarget.GetComponent<Character>(), out Vector3 predictedPosition, 2))
                    {
                        // we can autoaim, and we can predict! set the target
                        autoAimPredictedDirection = predictedPosition + Vector3.up * 0.5f - spawnPosition.position;
                        autoAimTarget = potentialAutoAimTarget.gameObject;
                    }
                }
            }

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

    private Character FindClosestTarget(float angleLimit)
    {
        Vector3 aimDirection = player.liveInput.aimDirection;
        float bestDot = Mathf.Cos(angleLimit * Mathf.Deg2Rad);
        Character bestTarget = null;

        foreach (Character player in Netplay.singleton.players)
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

    private bool PredictTargetPosition(Character target, out Vector3 predictedPosition, float maxPredictionTime)
    {
        float interval = 0.07f;
        Ticker<PlayerInput, CharacterState> ticker = target.ticker;
        Vector3 startPosition = spawnPosition.position;
        float ringDistance = 0f; // theoretical thrown ring distance
        float ringSpeed = effectiveWeaponSettings.projectileSpeed * interval;
        float lastTargetDistance = Vector3.Distance(target.transform.position, startPosition);
        Vector3 lastTargetPosition = target.transform.position;
        float originalTime = ticker.playbackTime;
        bool succeeded = false;

        predictedPosition = target.transform.position;

        for (int i = 1; i * interval < maxPredictionTime; i++)
        {
            ticker.Seek(originalTime + i * interval, ticker.realtimePlaybackTime, TickerSeekFlags.IgnoreDeltas | TickerSeekFlags.DontConfirm);
            ringDistance += ringSpeed;

            float currentTargetDistance = Vector3.Distance(target.transform.position, startPosition);

            if (currentTargetDistance >= lastTargetDistance + ringSpeed) // target is moving away faster than our ring would, so if they continue along this path we probably can't hit them
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

        ticker.Seek(originalTime, ticker.realtimePlaybackTime, TickerSeekFlags.IgnoreDeltas);

        return succeeded;
    }

    public void AddWeaponAmmo(RingWeaponSettingsAsset weaponType, bool doOverrideAmmo, float ammoOverride)
    {
        float ammoToAdd = doOverrideAmmo ? ammoOverride : weaponType.settings.timeOnPickup;

        if (MatchState.Get(out MatchConfiguration config))
        {
            if (config.weaponAmmoStyle == WeaponAmmoStyle.Quantity)
                ammoToAdd = weaponType.settings.ammoOnPickup;
        }

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

    [Command(channel = Channels.Reliable)] // our weapon selection should be reliable ideally!
    private void CmdSelectWeapon(RingWeaponSettingsAsset[] weaponTypes)
    {
        _localSelectedWeapons = weaponTypes;
        RegenerateEffectiveWeapon();
    }

    private bool CanThrowRing(float lenience) => player.numRings > 0 && Time.time - lastFiredRingTime >= 1f / effectiveWeaponSettings.shotsPerSecond - lenience;

    private void LocalThrowRing()
    {
        if (CanThrowRing(0f))
        {
            Vector3 direction = player.liveInput.aimDirection;

            if (autoAimTarget)
                direction = autoAimPredictedDirection;

            // Predict ring spawn
            Spawner.SpawnPrediction prediction = Spawner.MakeSpawnPrediction();
            OnCmdThrowRing(spawnPosition.position, direction, prediction, GameTicker.singleton ? GameTicker.singleton.predictedServerTime : 0f);

            if (!NetworkServer.active)
                CmdThrowRing(spawnPosition.position, direction, prediction, GameTicker.singleton ? GameTicker.singleton.predictedServerTime : 0f);

            hasFiredOnThisClick = true;
        }
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, float predictedServerTime)
    {
        float timeToThrowAt = predictedServerTime;
        if (timeToThrowAt > Time.realtimeSinceStartup)
        {
            // the client threw this, in what they predicted ahead of current time... this means we need to delay the shot until roughly the correct time arrives
            bufferedThrowEvents.Insert(timeToThrowAt, () => OnCmdThrowRing(position, direction, spawnPrediction, predictedServerTime));
        }
        else
        {
            // throw it now then
            OnCmdThrowRing(position, direction, spawnPrediction, predictedServerTime);
        }
    }

    private void OnCmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, float predictedServerTime)
    {
        if (!CanThrowRing(0.01f) || Vector3.Distance(position, spawnPosition.position) > 2f)
        {
            Log.WriteWarning($"Discarding ring throw: dist is {Vector3.Distance(position, spawnPosition.position)} CanThrow: {CanThrowRing(0.01f)}");
            return; // invalid throw
        }

        // spawn the predicted (client) or final (server) ring
        GameObject ring = Spawner.StartSpawn(effectiveWeaponSettings.prefab, position, Quaternion.identity, ref spawnPrediction);
        if (ring != null)
            FireSpawnedRing(ring, position, direction);
        Spawner.FinalizeSpawn(ring);

        if (effectiveWeaponSettings.additionalSpawns != null && effectiveWeaponSettings.additionalSpawns.Length > 0)
        {
            Vector3 right = Vector3.Cross(direction, movement.up).normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;

            foreach (AdditionalRingSpawn spawn in effectiveWeaponSettings.additionalSpawns)
            {
                Vector3 currentDirection = Quaternion.AngleAxis(spawn.horizontalAngleOffset, up) * Quaternion.AngleAxis(spawn.verticalAngleOffset, right) * direction;

                GameObject addRing = Spawner.StartSpawn(effectiveWeaponSettings.prefab, position, Quaternion.identity, ref spawnPrediction);
                if (addRing != null)
                    FireSpawnedRing(addRing, position + right * spawn.horizontalPositionOffset + up * spawn.verticalPositionOffset, currentDirection);
                Spawner.FinalizeSpawn(addRing);
            }
        }

        // Update stats
        if (NetworkServer.active)
        {
            player.numRings--;

            if (MatchState.Get(out MatchConfiguration matchConfig) && matchConfig.weaponAmmoStyle == WeaponAmmoStyle.Quantity)
            {
                // consume ammo
                for (int i = 0; i < weapons.Count; i++)
                {
                    if (!weapons[i].isInfinite && IsWeaponEquipped(weapons[i].weaponType))
                    {
                        RingWeapon modified = weapons[i];
                        modified.ammo = (int)(modified.ammo - 0.999f); // avoid rounding errors
                        weapons[i] = modified;
                    }
                }
            }
        }
    }

    private void FireSpawnedRing(GameObject ring, Vector3 position, Vector3 direction)
    {
        ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();
        Debug.Assert(ringAsThrownRing);

        ringAsThrownRing.Throw(player, position, direction);

        lastFiredRingTime = Time.time;
    }

    private void OnWeaponsListChanged(SyncList<RingWeapon>.Operation op, int itemIndex, RingWeapon oldItem, RingWeapon newItem)
    {
        RegenerateEffectiveWeapon();
    }

    private bool HasWeapon(RingWeaponSettingsAsset asset)
    {
        foreach (RingWeapon weapon in weapons)
        {
            if (weapon.weaponType == asset)
                return true;
        }
        return false;
    }

    private bool IsWeaponEquipped(RingWeaponSettingsAsset asset)
    {
        return localSelectedWeapons == null || localSelectedWeapons.Length == 0 || Array.IndexOf(localSelectedWeapons, asset) >= 0 || asset == defaultWeapon.weaponType;
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

        // start with primaries filled with all combinable weapon candidates
        foreach (var weapon in weapons)
        {
            if (IsWeaponEquipped(weapon.weaponType))
                primaries.Add(weapon.weaponType);
        }

        // determine the primary weapon to use
        for (int current = 0; current < primaries.Count; current++)
        {
            bool shouldExcludeCurrent = false;
            for (int other = 0; other < primaries.Count; other++)
            {
                if (other == current)
                    continue;

                foreach (var comboSettings in primaries[other].settings.comboSettings)
                {
                    // this other weapon contains this primary as a modifier (effector) so this other weapon is higher prio and more likely to become a primary
                    if (comboSettings.effector == primaries[current])
                        shouldExcludeCurrent = true;
                }
            }

            if (shouldExcludeCurrent)
            {
                // move from primary to effector
                effectors.Add(primaries[current]);
                primaries.RemoveAt(current--);
            }
        }

        if (primaries.Count > 0)
        {
            // select the second weapon (first is always the default red ring) as the primary
            effectiveWeaponSettings = primaries[Mathf.Min(1, primaries.Count - 1)].settings.Clone();

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
        }
        else
            Log.WriteWarning("Cannot generate primary weapon: there is none.");
    }
}