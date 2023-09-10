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
    /// Weapons currently equipped by the player. For local player it's locally predicted, for other players it's received form the server.
    /// </summary>
    public IList<RingWeaponSettingsAsset> equippedWeapons
    {
        get
        {
            if (!NetworkServer.active && !hasAuthority)
                return _remoteEquippedWeapons;
            else
                return _localEquippedWeapons;
        }
    }

    // Local selected weapons (local player + server)
    private List<RingWeaponSettingsAsset> _localEquippedWeapons = new List<RingWeaponSettingsAsset>();

    // Remote selected weapons (other player on client)
    private SyncList<RingWeaponSettingsAsset> _remoteEquippedWeapons = new SyncList<RingWeaponSettingsAsset>();

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

    [Header("Weapon selection")]
    public RingWeaponSettingsAsset wepKeyNone;
    public RingWeaponSettingsAsset wepKeyAuto;
    public RingWeaponSettingsAsset wepKeyBomb;
    public RingWeaponSettingsAsset wepKeyScatter;
    public RingWeaponSettingsAsset wepKeyGrenade;
    public RingWeaponSettingsAsset wepKeyRail;
    public RingWeaponSettingsAsset wepKeyAim;

    public GameObject autoAimTarget { get;  private set; }
    private Vector3 autoAimPredictedDirection = Vector3.zero;
    public List<Vector3> autoaimPredictedBlips = new List<Vector3>(30);

    // Components
    private Character character;
    private PlayerCharacterMovement movement;

    private TimelineTrack<Action> bufferedThrowEvents = new TimelineTrack<Action>();

    public float testAutoAimSmoothDamp = 0.1f;
    private Vector3 autoAimDampVelocity;

    void Awake()
    {
        character = GetComponent<Character>();
        movement = GetComponent<PlayerCharacterMovement>();
    }

    private void Start()
    {
        weapons.Callback += OnWeaponsListChanged;
        _remoteEquippedWeapons.Callback += OnRemoteEquippedWeaponsChanged;
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
        UpdateWeaponSelectHotkeys();

        UpdateAutoAim();

        for (int i = bufferedThrowEvents.Count - 1; i >= 0; i--)
        {
            if (GameTicker.singleton.predictedServerTime >= bufferedThrowEvents.TimeAt(i))
            {
                bufferedThrowEvents[i].Invoke();
                bufferedThrowEvents.RemoveAt(i);
            }
        }

        // Fire weapons if we can
        if (hasAuthority && character.liveInput.btnFire && (!hasFiredOnThisClick || effectiveWeaponSettings.isAutomatic))
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

        hasFiredOnThisClick &= character.liveInput.btnFire;
    }

    private void UpdateWeaponSelectHotkeys()
    {
        if (hasAuthority)
        {
            PlayerControls inputs = GameManager.singleton.input;

            // pretty much this
            if (inputs.Gameplay.WepNone.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyNone });
            if (inputs.Gameplay.WepAuto.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyAuto });
            if (inputs.Gameplay.WepBomb.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyBomb });
            if (inputs.Gameplay.WepScatter.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyScatter });
            if (inputs.Gameplay.WepGrenade.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyGrenade });
            if (inputs.Gameplay.WepAim.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyAim });
            if (inputs.Gameplay.WepRail.ReadValue<float>() > 0.5f) LocalSetSelectedWeapons(new[] { wepKeyRail });
        }
    }

    private void UpdateAutoAim()
    {
        autoAimTarget = null;

        if (hasAuthority && effectiveWeaponSettings.autoAimHitboxRadius > 0f && effectiveWeaponSettings.autoAimDegreesRadius > 0f)
        {
            Character potentialAutoAimTarget = FindClosestTarget(effectiveWeaponSettings.autoAimDegreesRadius);

            if (potentialAutoAimTarget)
            {
                Vector3 targetPosAdjusted = potentialAutoAimTarget.transform.position + Vector3.up * 0.5f;
                Vector3 nearTargetPoint = spawnPosition.position + character.liveInput.aimDirection * Vector3.Dot(character.liveInput.aimDirection, targetPosAdjusted - spawnPosition.position);

                if (Vector3.Distance(nearTargetPoint, targetPosAdjusted) <= effectiveWeaponSettings.autoAimHitboxRadius)
                {
                    if (PredictTargetPosition(potentialAutoAimTarget.GetComponent<Character>(), out Vector3 predictedPosition, 2))
                    {
                        // we can autoaim, and we can predict! set the target
                        autoAimPredictedDirection = (predictedPosition + Vector3.up * 0.5f) - spawnPosition.position;
                        autoAimTarget = potentialAutoAimTarget.gameObject;
                    }
                }
            }
        }
    }

    private Character FindClosestTarget(float angleLimit)
    {
        Vector3 aimDirection = character.liveInput.aimDirection;
        float bestDot = Mathf.Cos(angleLimit * Mathf.Deg2Rad);
        Character bestTarget = null;

        foreach (Character player in Netplay.singleton.players)
        {
            if (player && player != this.character && player.damageable.CanBeDamagedBy(this.character.damageable.damageTeam))
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

    public bool PredictTargetPosition(Character target, out Vector3 predictedPosition, float maxPredictionTime)
    {
        Timeline.Entity<CharacterState, CharacterInput> targetEntity = target.entity;
        Vector3 startPosition = spawnPosition.position;
        Vector3 lastTargetPosition = target.transform.position;
        bool succeeded = false;
        const float interval = 0.1f;
        CharacterState targetEntityOriginalState = targetEntity.target.MakeState();

        predictedPosition = target.transform.position;

        autoaimPredictedBlips.Add(predictedPosition);
        autoaimPredictedBlips.Clear();

        // Throw an imaginary ring and find where it'll intersect with the target entity
        float ringDistance = 0f;
        float ringStep = effectiveWeaponSettings.projectileSpeed * interval;
        float lastTargetDistance = Vector3.Distance(target.transform.position, startPosition);
        for (int i = 1; i * interval < maxPredictionTime; i++)
        {
            // Tick the player
            targetEntity.GenericTick(interval, 0, 0, new TickInfo() { isForwardTick = false, isFullTick = false });
            // Tick the imaginary ring we'll fire
            ringDistance += ringStep;

            Vector3 currentTargetPosition = target.transform.position;
            float currentTargetDistance = Vector3.Distance(currentTargetPosition, startPosition);

            autoaimPredictedBlips.Add(currentTargetPosition + new Vector3(0, 0.5f, 0));

            if (currentTargetDistance >= lastTargetDistance + ringStep) // target is moving away faster than our ring would, so if they continue along this path we probably can't hit them
                break;

            if (ringDistance >= currentTargetDistance)
            {
                // the ring is normally a bit ahead, estimate a position between the last and current position using the typical gap per interval as a point of reference
                float blend = 1f - (ringDistance - currentTargetDistance) / ringStep;
                predictedPosition = Vector3.LerpUnclamped(lastTargetPosition, currentTargetPosition, blend);
                succeeded = true;
                break;
            }

            lastTargetDistance = currentTargetDistance;
            lastTargetPosition = currentTargetPosition;
        }

        // Restore the entity's old state
        targetEntity.target.ApplyState(targetEntityOriginalState);

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

    public void LocalSetSelectedWeapons(IEnumerable<RingWeaponSettingsAsset> weapons)
    {
        if (hasAuthority)
        {
            if (!isServer)
            {
                CmdSelectWeapon(new List<RingWeaponSettingsAsset>(weapons).ToArray()); // ask the server for new weapons plz
            }
            else
            {
                _remoteEquippedWeapons.Clear();
                _remoteEquippedWeapons.AddRange(weapons);
            }

            _localEquippedWeapons.Clear();
            _localEquippedWeapons.AddRange(weapons);
            RegenerateEffectiveWeapon();
        }
    }

    [Command(channel = Channels.Reliable)] // our weapon selection should be reliable ideally!
    private void CmdSelectWeapon(RingWeaponSettingsAsset[] weaponTypes)
    {
        _remoteEquippedWeapons.Clear();
        _remoteEquippedWeapons.AddRange(weaponTypes);
        _localEquippedWeapons.Clear();
        _localEquippedWeapons.AddRange(weaponTypes);
        RegenerateEffectiveWeapon();
    }

    public bool CanThrowRing(float lenience) => character.numRings > 0 && Time.time - lastFiredRingTime >= 1f / effectiveWeaponSettings.shotsPerSecond - lenience;

    public void LocalThrowRing()
    {
        if (CanThrowRing(0f))
        {
            Vector3 direction = character.liveInput.aimDirection;

            if (autoAimTarget)
                direction = autoAimPredictedDirection;

            // Predict ring spawn
            Spawner.SpawnPrediction prediction = Spawner.MakeSpawnPrediction();
            double timeOfThrow = GameTicker.singleton ? GameTicker.singleton.predictedServerTime : 0f;
            double replicaTimeOfThrow = GameTicker.singleton ? GameTicker.singleton.predictedReplicaServerTime : 0f;

            OnCmdThrowRing(spawnPosition.position, direction, prediction, timeOfThrow, replicaTimeOfThrow);

            if (!NetworkServer.active)
                CmdThrowRing(spawnPosition.position, direction, prediction, timeOfThrow, (float)(replicaTimeOfThrow - timeOfThrow));

            hasFiredOnThisClick = true;
        }
    }

    [Command(channel = Channels.Unreliable)]
    private void CmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, double predictedServerTime, float predictedReplicaServerTimeOffset)
    {
        if (predictedServerTime > GameTicker.singleton.predictedServerTime)
        {
            // the client threw this, in what they predicted ahead of current time... this means we need to delay the shot until roughly the correct time arrives
            bufferedThrowEvents.Insert(predictedServerTime, () => OnCmdThrowRing(position, direction, spawnPrediction, predictedServerTime, predictedServerTime + predictedReplicaServerTimeOffset));
        }
        else
        {
            // throw it now then
            OnCmdThrowRing(position, direction, spawnPrediction, predictedServerTime, predictedServerTime + predictedReplicaServerTimeOffset);
        }
    }

    private void OnCmdThrowRing(Vector3 position, Vector3 direction, Spawner.SpawnPrediction spawnPrediction, double predictedServerTime, double predictedReplicaServerTime)
    {
        // Verify the ring throw
        float characterPositionDisparity = Vector3.Distance(position, spawnPosition.position);

        /*if (ServerState.instance.serverRewindTolerance > 0f)
        {
            // ring may have been thrown in the past WHEEEEEEE test it to see if it's good
            int earlierPositionIndex = character.ticker.stateTimeline.ClosestIndexBefore(Mathf.Max(predictedReplicaServerTime, GameTicker.singleton.predictedServerTime - ServerState.instance.serverRewindTolerance));
            if (earlierPositionIndex != -1)
            {
                CharacterState oldState = character.ticker.stateTimeline[earlierPositionIndex];
                Matrix4x4 oldCharacterTransform = Matrix4x4.TRS(oldState.position, oldState.rotation, character.transform.localScale);
                characterPositionDisparity = Vector3.Distance(position, (oldCharacterTransform * character.transform.worldToLocalMatrix).MultiplyPoint(Vector3.zero));
            }
        }*/

        if (!CanThrowRing(0.01f) || characterPositionDisparity > 1f)
        {
            Log.WriteWarning($"Discarding ring throw: dist is {characterPositionDisparity.ToString("F2")} CanThrow: {CanThrowRing(0.01f)}");
            return; // invalid throw
        }

        // spawn the predicted (client) or final (server) ring
        GameObject ring = Spawner.StartSpawn(effectiveWeaponSettings.prefab, position, Quaternion.identity, ref spawnPrediction);
        if (ring != null)
            FireSpawnedRing(ring, position, direction, predictedServerTime, predictedReplicaServerTime);
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
                    FireSpawnedRing(addRing, position + right * spawn.horizontalPositionOffset + up * spawn.verticalPositionOffset, currentDirection, predictedServerTime, predictedReplicaServerTime);
                Spawner.FinalizeSpawn(addRing);
            }
        }

        if (NetworkServer.active)
        {
            // Update stats
            character.numRings--;

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

    private void FireSpawnedRing(GameObject ring, Vector3 position, Vector3 direction, double predictedServerTime, double predictedReplicaServerTime)
    {
        ThrownRing ringAsThrownRing = ring.GetComponent<ThrownRing>();
        Debug.Assert(ringAsThrownRing);

        float serverPredictionAmount = 0f;
        if (NetworkServer.active && GameState.Get(out GameState_ServerSettings gsServerSettings))
            serverPredictionAmount = Mathf.Min(gsServerSettings.settings.hitLagCompensation, (float)(GameTicker.singleton.predictedServerTime - predictedReplicaServerTime)); // 0 if serverRewindTolerance is 0

        ringAsThrownRing.Throw(character, position, direction, serverPredictionAmount);

        lastFiredRingTime = Time.time;
    }

    private void OnWeaponsListChanged(SyncList<RingWeapon>.Operation op, int itemIndex, RingWeapon oldItem, RingWeapon newItem)
    {
        RegenerateEffectiveWeapon();
    }

    private void OnRemoteEquippedWeaponsChanged(SyncList<RingWeaponSettingsAsset>.Operation op, int itemIndex, RingWeaponSettingsAsset oldItem, RingWeaponSettingsAsset newItem)
    {
        if (!NetworkServer.active && !hasAuthority)
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
        return equippedWeapons == null || equippedWeapons.Count == 0 || equippedWeapons.IndexOf(asset) >= 0 || asset == defaultWeapon.weaponType;
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