using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class Character : NetworkBehaviour, ITickable<CharacterState, CharacterInput>
{
    [System.Serializable]
    public struct RingDropLayer
    {
        public float verticalSpeed;
        public float horizontalSpeed;
        public int maxNumRings;
    }

    [Header("Player info")]
    /// <summary>
    /// Name of this player. By default, all players are Fred.
    /// </summary>
    [SyncVar(hook=nameof(OnPlayerNameChanged))] public string playerName = "Fred";

    /// <summary>
    /// Player ID
    /// </summary>
    [SyncVar(hook=nameof(OnPlayerIdChanged))] public int playerId;

    /// <summary>
    /// Owning player. Server-only
    /// </summary>
    public Player serverOwningPlayer;

    [SyncVar]
    public GameObject shield;

    /// <summary>
    /// Is this the locally-controlled player?
    /// </summary>
    private bool isLocal => playerId == Netplay.singleton.localPlayerId;

    /// <summary>
    /// Current inputs of this player. If this is the local player, inputs may be slightly later inputs than the player last processed.
    /// </summary>
    public CharacterInput liveInput => isLocal ? (GameTicker.singleton != null ? GameTicker.singleton.localPlayerInput : default) : (entity != null ? entity.inputTrack.Latest : default);

    /// <summary>
    /// Time of this player
    /// </summary>
    public float time;

    [Header("Team and identity")]
    [SyncVar(hook = nameof(OnColourChanged))]
    public Color colour;

    [SyncVar(hook = nameof(OnTeamChanged))]
    private PlayerTeam _team = PlayerTeam.None;

    public PlayerTeam team
    {
        set
        {
            _team = value;
            damageable.damageTeam = (int)_team;
            OnColourChanged(colour, colourByTeam[(int)team]);
        }
        get => _team;
    }

    public Color[] colourByTeam = new Color[0];

    [Header("Shinies")]
    [SyncVar]
    public int score = 0;

    public bool isFirstPlace
    {
        set
        {
            if (crownModel)
            {
                if (value != crownModel.gameObject.activeSelf)
                    crownModel.gameObject.SetActive(value);
            }
            _isFirstPlace = value;
        }
        get => _isFirstPlace;
    }
    private bool _isFirstPlace = false;

    /// <summary>
    /// Number of rings picked up
    /// </summary>
    public int numRings { set => _numRings = NetworkServer.active ? value : _numRings; get => _numRings; }
    [SyncVar]
    private int _numRings;

    [Header("Visuals")]
    public Renderer characterModel;
    public Renderer crownModel;
    public Transform flagHoldBone;
    public Color allyOutlineColour = Color.blue;
    public Color enemyOutlineColour = Color.red;
    public float localPlayerAlpha = 0.4f;

    [Header("Ring drop")]
    public GameObject droppedRingPrefab;
    public Transform droppedRingSpawnPoint;

    public RingDropLayer[] ringDropLayers = new RingDropLayer[0];

    [Tooltip("Number of weapons dropped is capped at this amount")]
    public int maxNumWeaponsToDrop = 3;
    [Tooltip("The number of weapons to drop on average, as a percentage of the weapons carried. Note that the cap still applies.")]
    public float averageWeaponDropPercentage = 50f;
    [Tooltip("Ring layer to drop weapons in")]
    public RingDropLayer weaponRingDropLayer = new RingDropLayer();

    [Header("Hurt")]
    public float hurtDefaultHorizontalKnockback = 5;
    public float hurtDefaultVerticalKnockback = 5;

    [Header("Misc")]
    public float killY = -50f;

    // Components
    [HideInInspector] public PlayerCharacterMovement movement;
    [HideInInspector] public Damageable damageable;
    private PlayerSounds sounds;

    // Timeline entity - handles ticking the player
    [HideInInspector]
    public Timeline.Entity<CharacterState, CharacterInput> entity;

    public float timeOfLastInputPush { get; set; }

    public bool isInvisible { get; set; }

    /// <summary>Note: overridden by isInvisible</summary>
    public float renderAlpha
    {
        get => _renderAlpha;
        set
        {
            if (_renderAlpha != value)
            {
                _renderAlpha = value;
                characterModel.material.SetFloat("_Alpha", _renderAlpha);
            }
        }
    }
    private float _renderAlpha = 1f;

    public bool isHoldingFlag => holdingFlag != null;

    private static int nextSpawner = 0;

    public float localTime = -1;

    public int characterIndex { get; set; }

    public TheFlag holdingFlag
    {
        get
        {
            if (MatchState.Get(out MatchFlags matchCtf) && matchCtf.redFlag && matchCtf.blueFlag)
            {
                if (matchCtf.blueFlag.currentCarrier == playerId)
                    return matchCtf.blueFlag;
                if (matchCtf.redFlag.currentCarrier == playerId)
                    return matchCtf.redFlag;
            }
            return null;
        }
    }

    void Awake()
    {
        movement = GetComponent<PlayerCharacterMovement>();
        damageable = GetComponent<Damageable>();
        sounds = GetComponent<PlayerSounds>();
    }

    void Start()
    {
        PlayerControls control = new PlayerControls();

        damageable.onLocalDamaged.AddListener(OnDamaged);

        OnColourChanged(colour, colour); // HACK: we need to update visuals

        GamePreferences.onPreferencesChanged += OnPreferencesChanged;
        if (TryGetComponent(out TimelineEntityInterpolator interpolator))
            interpolator.mispredictionInterpolationSmoothing = GamePreferences.opponentSmoothing;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        Netplay.singleton.RegisterPlayer(this);
        WhenReady<GameTicker>.Execute(this, ticker => RegisterEntity(ticker));

        if (MatchState.Get(out MatchTeams matchTeams))
            ChangeTeam(matchTeams.FindBestTeamToJoin());

        Respawn();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Netplay.singleton.RegisterPlayer(this, playerId);
        if (!NetworkServer.active) // don't call twice on host
            WhenReady<GameTicker>.Execute(this, ticker => RegisterEntity(ticker));

        if (!hasAuthority)
            UpdateOutlineColour();
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
    }

    private void OnDestroy()
    {
        GamePreferences.onPreferencesChanged -= OnPreferencesChanged;

        if (NetworkServer.active)
        {
            if (holdingFlag != null)
                holdingFlag.ReturnToBase(true);
        }

        WhenReady<GameTicker>.Execute(this, ticker => ticker.UnregisterEntity(this));
    }

    void Update()
    {
        if (isInvisible)
            characterModel.enabled = crownModel.enabled = false;
        else if (!damageable.isInvincible) // blinking also controls visibility so we won't change it while invincible
            characterModel.enabled = crownModel.enabled = true;

        if (PlayerCamera.singleton && PlayerCamera.singleton.currentPlayer == this && !GameManager.singleton.isPaused /* allow character customisation */)
            renderAlpha = localPlayerAlpha;
        else
            renderAlpha = 1f;

        if (isServer && transform.position.y < killY)
        {
            damageable.TryDamage(gameObject, Vector3.zero, false);

            MessageFeed.Post($"<player>{playerName}</player> fell off the world!");

            Respawn();
        }
    }

    public void Tick(float deltaTime, CharacterInput input, TickInfo tickInfo)
    {
        movement.TickMovement(deltaTime, input, tickInfo);
    }

    private void RegisterEntity(GameTicker ticker)
    {
        entity = ticker.RegisterEntity(this, this);
        if (TryGetComponent(out TimelineEntityInterpolator interpolator))
            interpolator.SetOwningEntity(entity, x => x.position);
    }

    public void Respawn()
    {
        if (NetworkServer.active)
        {
            List<PlayerSpawn> spawners = new List<PlayerSpawn>(GameObject.FindObjectsByType<PlayerSpawn>(FindObjectsSortMode.None));

            spawners.RemoveAll(s => s.team != team);

            if (spawners.Count > 0)
            {
                PlayerSpawn spawnPoint = spawners[(nextSpawner++) % spawners.Count];

                transform.position = spawnPoint.transform.position;
                transform.forward = spawnPoint.transform.forward.Horizontal(); // todo

                movement.velocity = Vector3.zero;
                movement.state = CharacterMovementState.None;

                if (entity != null)
                    entity.StoreCurrentState(entity.latestStateTime);

                TargetRespawn(spawnPoint.transform.forward);
            }
            else
            {
                Log.WriteWarning("No player spawners compatible with this team in this stage!");
            }
        }
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void TargetRespawn(Vector3 direction)
    {
        if (isLocal)
        {
            GameTicker.singleton.localPlayerInput.aimDirection = direction.Horizontal().normalized;
        }
    }

    private void OnDamaged(GameObject instigator, Vector3 force, bool instaKill)
    {
        if (instaKill)
        {
            entity.owner.CallEvent((TickInfo tickInfo) => 
            {
                if (tickInfo.isFullForwardTick)
                    Respawn();
            });
        }
        else
        {
            if (force.sqrMagnitude <= Mathf.Epsilon)
                force = -transform.forward.Horizontal() * hurtDefaultHorizontalKnockback;
            else if (force.Horizontal().magnitude < hurtDefaultHorizontalKnockback)
                force.SetHorizontal(force.Horizontal().normalized * hurtDefaultHorizontalKnockback);
            force += movement.up * hurtDefaultVerticalKnockback;

            // predict our hit
            entity.owner.CallEvent((_) => movement.ApplyHitKnockback(force));

            // only the server can do the rest (ring drop, score, etc)
            if (NetworkServer.active)
            {
                if (instigator && instigator.TryGetComponent(out Character attackerAsPlayer))
                {
                    // Give score to the attacker if possible
                    if (attackerAsPlayer)
                        attackerAsPlayer.score += isFirstPlace ? 100 : 50; // double points if the person hit has a crown!

                    // Add the hit message
                    MessageFeed.Post($"<player>{attackerAsPlayer.playerName}</player> hit <player>{playerName}</player> with a {attackerAsPlayer.GetComponent<CharacterShooting>().effectiveWeaponSettings.name} ring!");
                }

                if (shield == null)
                {
                    // Drop some rings
                    DropRings();
                }
                else
                {
                    // Just lose the shield we have
                    LoseShield();
                }
            }
        }

        if (NetworkServer.active && holdingFlag)
        {
            holdingFlag.Drop();
        }
    }

    public void DropRings()
    {
        int numToDrop = 0;
        int numDropped = 0;

        for (int ringLayer = 0; ringLayer < ringDropLayers.Length; ringLayer++)
            numToDrop += ringDropLayers[ringLayer].maxNumRings;

        numToDrop = Mathf.Min(numToDrop, numRings);

        // Distribute dropped rings across the ring layers starting from 0
        for (int ringLayer = 0; ringLayer < ringDropLayers.Length; ringLayer++)
        {
            float horizontalVelocity = ringDropLayers[ringLayer].horizontalSpeed;
            float verticalVelocity = ringDropLayers[ringLayer].verticalSpeed;
            int currentNumToDrop = Mathf.Min(numToDrop - numDropped, ringDropLayers[ringLayer].maxNumRings);

            for (int i = 0; i < currentNumToDrop; i++)
            {
                float horizontalAngle = i * Mathf.PI * 2f / currentNumToDrop;
                Movement ringMovement = Spawner.StartSpawn(droppedRingPrefab, droppedRingSpawnPoint.position, Quaternion.identity).GetComponent<Movement>();
                Ring ring = ringMovement?.GetComponent<Ring>();

                Debug.Assert(ringMovement && ring);

                ringMovement.velocity = new Vector3(Mathf.Sin(horizontalAngle) * horizontalVelocity, verticalVelocity, Mathf.Cos(horizontalAngle) * horizontalVelocity);
                ring.isDroppedRing = true;

                Spawner.FinalizeSpawn(ringMovement.gameObject); // we want to set isDroppedRing before we send the spawn message, the clients need to know at the beginning what kind of ring it is.

                numDropped++;
            }
        }

        // Drop weapons
        CharacterShooting ringShooting = GetComponent<CharacterShooting>();
        if (ringShooting)
        {
            int numWeaponsToDrop = Mathf.CeilToInt((ringShooting.weapons.Count - 1) * averageWeaponDropPercentage / 100f - 0.001f);
            float baseDropAngle = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < numWeaponsToDrop; i++)
            {
                int indexToDrop = Random.Range(1, ringShooting.weapons.Count);

                if (indexToDrop < ringShooting.weapons.Count && ringShooting.weapons[indexToDrop].weaponType.settings.droppedRingPrefab)
                {
                    GameObject droppedWeapon = Spawner.StartSpawn(ringShooting.weapons[indexToDrop].weaponType.settings.droppedRingPrefab, droppedRingSpawnPoint.position, Quaternion.identity);
                    float angleToDrop = baseDropAngle + i * Mathf.PI * 2f / numWeaponsToDrop;

                    if (droppedWeapon && droppedWeapon.TryGetComponent(out RingWeaponPickup weaponPickup) && droppedWeapon.TryGetComponent(out Ring weaponRing) && droppedWeapon.TryGetComponent(out Movement ringMovement))
                    {
                        weaponPickup.overrideAmmo = true;
                        weaponPickup.ammo = ringShooting.weapons[indexToDrop].ammo;
                        weaponRing.isDroppedRing = true;

                        ringMovement.velocity = new Vector3(Mathf.Sin(angleToDrop) * weaponRingDropLayer.horizontalSpeed, weaponRingDropLayer.verticalSpeed, Mathf.Cos(angleToDrop) * weaponRingDropLayer.horizontalSpeed);

                        numDropped++;
                    }
                    else
                    {
                        Debug.LogWarning($"Dropped weapon {droppedWeapon} is missing some components. Make sure it has a WeaponPickup, Ring and Movement component!");
                    }

                    Spawner.FinalizeSpawn(droppedWeapon);

                    ringShooting.weapons.RemoveAt(indexToDrop);
                }
            }
        }

        if (numDropped > 0)
            sounds.PlayNetworked(PlayerSounds.PlayerSoundType.RingDrop);
        else
            sounds.PlayNetworked(PlayerSounds.PlayerSoundType.ShieldLoss); // temp, better than hearing nothing

        numRings = 0;
    }

    private static readonly string[] nameSuffices = { " and Knuckles", " Jr", " Sr", " Classic", " Modern", " Esquire", " Ph.d", " Squared" }; // ive done my best

    public void Rename(string newName)
    {
        string updatedName = newName;
        int currentSuffix = 0;

        if (string.IsNullOrWhiteSpace(newName))
            newName = updatedName = "Anonymous";

        while (Netplay.singleton.players.Exists(a => a != null && a != this && a.playerName == updatedName))
        {
            if (currentSuffix < nameSuffices.Length)
                updatedName = newName + nameSuffices[currentSuffix++];
            else
                updatedName = newName + nameSuffices[currentSuffix / nameSuffices.Length] + nameSuffices[currentSuffix % nameSuffices.Length]; // crap lol itll be fine ok
        }

        playerName = updatedName;
        gameObject.name = playerName;
    }

    [Server]
    public void TryChangeColour(Color newColour)
    {
        if (team == PlayerTeam.None)
        {
            OnColourChanged(colour, newColour);
        }
    }

    [Server]
    public void ChangeTeam(PlayerTeam team)
    {
        this.team = team;
    }

    public Color GetCharacterColour()
    {
        if (colour.r == 0 && colour.g == 0 && colour.b == 0)
            return characterModel.material.GetColor("_SourceColor");
        else
            return colour;
    }

    public void UpdateOutlineColour()
    {
        PlayerTeam localTeam = Netplay.singleton.localPlayer ? Netplay.singleton.localPlayer.team : PlayerTeam.None;

        characterModel.material.SetColor("_OutlineColor", (localTeam != team || team == PlayerTeam.None) ? enemyOutlineColour : allyOutlineColour);
    }

    private void OnColourChanged(Color oldColour, Color newColour)
    {
        colour = newColour;
        characterModel.material.color = newColour;
    }

    private void OnPlayerNameChanged(string oldVal, string newVal)
    {
        playerName = newVal;
        gameObject.name = playerName;
    }

    private void OnTeamChanged(PlayerTeam oldTeam, PlayerTeam newTeam)
    {
        team = newTeam;

        if (Netplay.singleton.localPlayer)
        {
            if (this == Netplay.singleton.localPlayer) // we've changed team, now we need to update everyone else's outline
            {
                foreach (Character character in Netplay.singleton.players)
                {
                    if (character)
                        character.UpdateOutlineColour();
                }
            }
            else
            {
                UpdateOutlineColour();
            }
        }
    }

    private void OnPlayerIdChanged(int oldVal, int newVal)
    {
        playerId = newVal;

        Netplay.singleton.RegisterPlayer(this, newVal);
    }

    [Server]
    public void ApplyShield(GameObject shieldPrefab)
    {
        if (shield)
            LoseShield();

        shield = Spawner.Spawn(shieldPrefab);

        if (shield.TryGetComponent(out Shield shieldComponent))
            shieldComponent.target = gameObject;

        sounds.PlayNetworked(PlayerSounds.PlayerSoundType.ShieldGain);
    }

    [Server]
    public void LoseShield()
    {
        if (shield)
        {
            Spawner.Despawn(shield);
            shield = null;

            sounds.PlayNetworked(PlayerSounds.PlayerSoundType.ShieldLoss);
        }
    }

    private void OnPreferencesChanged()
    {
        if (TryGetComponent(out TimelineEntityInterpolator interpolator))
            interpolator.mispredictionInterpolationSmoothing = GamePreferences.opponentSmoothing;
    }

    /// <summary>
    /// Makes a state package
    /// </summary>
    public CharacterState MakeState()
    {
        return new CharacterState()
        {
            position = transform.position,
            rotation = transform.rotation,
            state = movement.state,
            velocity = movement.velocity,
            up = movement.up,
            stateFloat = movement.stateFloat
        };
    }

    /// <summary>
    /// Applies a state package to our actual state
    /// </summary>
    /// <param name="state"></param>
    public void ApplyState(CharacterState state)
    {
        transform.position = state.position;
        transform.rotation = state.rotation;
        movement.state = state.state;
        movement.velocity = state.velocity;
        movement.up = state.up;
        movement.stateFloat = state.stateFloat;

        Physics.SyncTransforms(); // CRUCIAL for correct collision checking - a lot of things broke before adding this...
    }
}

public enum PlayerTeam
{
    None,
    Red,
    Blue
};

public static class PlayerTeamExtensions
{
    public static string ToFontColor(this PlayerTeam team)
    {
        switch (team)
        {
            case PlayerTeam.Red:
                return "<color=red>";
            case PlayerTeam.Blue:
                return "<color=blue>";
        }

        return "<color=#7f7f7f>";
    }

    public static string ToColoredString(this PlayerTeam team)
    {
        switch (team)
        {
            case PlayerTeam.Red:
                return "<color=red>red</color>";
            case PlayerTeam.Blue:
                return "<color=blue>blue</color>";
        }

        return "<color=7f7f7f>neutral</color>";
    }
}