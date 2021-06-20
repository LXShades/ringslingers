using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class Character : NetworkBehaviour
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

    [SyncVar]
    public GameObject shield;

    /// <summary>
    /// Is this the locally-controlled player?
    /// </summary>
    private bool isLocal => playerId == Netplay.singleton.localPlayerId;

    /// <summary>
    /// Current inputs of this player. If this is the local player, inputs may be slightly later inputs than the player last processed.
    /// </summary>
    public PlayerInput liveInput => isLocal ? GameTicker.singleton.localPlayerInput : ticker.inputHistory.Latest;

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
        }
        get => _team;
    }

    public Color[] colourByTeam = new Color[0];

    [Header("Shinies")]
    [SyncVar]
    public int score = 0;

    /// <summary>
    /// Number of rings picked up
    /// </summary>
    public int numRings { set => _numRings = NetworkServer.active ? value : _numRings; get => _numRings; }
    [SyncVar]
    private int _numRings;

    [Header("Visuals")]
    public Renderer characterModel;
    public Transform flagHoldBone;
    public TrailRenderer speedTrails;

    [Header("Ring drop")]
    public GameObject droppedRingPrefab;
    public Transform droppedRingSpawnPoint;

    public RingDropLayer[] ringDropLayers = new RingDropLayer[0];

    [Header("Hurt")]
    public float hurtDefaultHorizontalKnockback = 5;
    public float hurtDefaultVerticalKnockback = 5;

    [Header("Debug")]
    public bool logLocalPlayerReconciles = true;

    // Components
    /// <summary>
    /// Player movement component
    /// </summary>
    [HideInInspector] public CharacterMovement movement;
    [HideInInspector] public Damageable damageable;
    [HideInInspector] public Ticker ticker;
    private PlayerSounds sounds;

    public bool isInvisible { get; set; }

    public bool isHoldingFlag => holdingFlag != null;

    private static int nextSpawner = 0;

    public float localTime = -1;

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
        movement = GetComponent<CharacterMovement>();
        damageable = GetComponent<Damageable>();
        sounds = GetComponent<PlayerSounds>();
        ticker = GetComponent<Ticker>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        Netplay.singleton.RegisterPlayer(this);

        if (MatchState.Get(out MatchTeams matchTeams))
            ChangeTeam(matchTeams.FindBestTeamToJoin());

        Respawn();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Netplay.singleton.RegisterPlayer(this, playerId);
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();

        if (logLocalPlayerReconciles)
            ticker.debugLogReconciles = true;
    }

    void Start()
    {
        PlayerControls control = new PlayerControls();

        damageable.onLocalDamaged.AddListener(OnDamaged);

        OnColourChanged(colour, colour); // HACK: we need to update visuals
    }

    void Update()
    {
        if (isInvisible)
            characterModel.enabled = false;
        else if (!damageable.isInvincible) // blinking also controls visibility so we won't change it while invincible
            characterModel.enabled = true;
    }

    public void Respawn()
    {
        if (NetworkServer.active)
        {
            List<PlayerSpawn> spawners = new List<PlayerSpawn>(GameObject.FindObjectsOfType<PlayerSpawn>());

            spawners.RemoveAll(s => s.team != team);

            if (spawners.Count > 0)
            {
                PlayerSpawn spawnPoint = spawners[(nextSpawner++) % spawners.Count];

                transform.position = spawnPoint.transform.position;
                transform.forward = spawnPoint.transform.forward.Horizontal(); // todo

                movement.velocity = Vector3.zero;
                movement.state = 0;

                ticker.ConfirmCurrentState();

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
            ticker.CallEvent((bool isRealtime) => 
            {
                if (isRealtime)
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
            ticker.CallEvent((bool _) => movement.ApplyHitKnockback(force));

            // only the server can do the rest (ring drop, score, etc)
            if (NetworkServer.active)
            {
                if (instigator && instigator.TryGetComponent(out Character attackerAsPlayer))
                {
                    // Give score to the attacker if possible
                    if (attackerAsPlayer)
                        attackerAsPlayer.score += 50;

                    // Add the hit message
                    MessageFeed.Post($"<player>{attackerAsPlayer.playerName}</player> hit <player>{playerName}</player> with a {attackerAsPlayer.GetComponent<RingShooting>().effectiveWeaponSettings.name} ring!");
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
        RingShooting ringShooting = GetComponent<RingShooting>();
        if (ringShooting)
        {
            if (ringShooting.weapons.Count > 1 && ringShooting.weapons[1].weaponType.settings.droppedRingPrefab)
            {
                GameObject droppedWeapon = Spawner.Spawn(ringShooting.weapons[1].weaponType.settings.droppedRingPrefab, droppedRingSpawnPoint.position, Quaternion.identity);

                if (droppedWeapon && droppedWeapon.TryGetComponent(out RingWeaponPickup weaponPickup) && droppedWeapon.TryGetComponent(out Ring weaponRing))
                {
                    weaponPickup.overrideAmmo = true;
                    weaponPickup.ammo = ringShooting.weapons[1].ammo;
                    weaponRing.isDroppedRing = true;
                    ringShooting.weapons.RemoveAt(1);
                }
            }
        }

        if (numToDrop > 0)
            sounds.PlayNetworked(PlayerSounds.PlayerSoundType.RingDrop);

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
        OnColourChanged(colour, colourByTeam[(int)team]);
    }

    private void OnColourChanged(Color oldColour, Color newColour)
    {
        colour = newColour;
        characterModel.material.color = newColour;

        if (newColour.r == 0 && newColour.g == 0 && newColour.b == 0)
        {
            Color defaultColour = characterModel.material.GetColor("_SourceColor");
            speedTrails.startColor = defaultColour;
            speedTrails.endColor = new Color(defaultColour.r, defaultColour.g, defaultColour.b, 0f);
        }
        else
        {
            speedTrails.startColor = newColour;
            speedTrails.endColor = new Color(newColour.r, newColour.g, newColour.b, 0f);
        }
    }

    private void OnPlayerNameChanged(string oldVal, string newVal)
    {
        playerName = newVal;
        gameObject.name = playerName;
    }

    private void OnTeamChanged(PlayerTeam oldTeam, PlayerTeam newTeam)
    {
        team = newTeam;
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
            spindashChargeLevel = movement.spindashChargeLevel
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
        movement.spindashChargeLevel = state.spindashChargeLevel;

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