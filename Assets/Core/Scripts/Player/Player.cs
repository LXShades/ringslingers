using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class Player : NetworkBehaviour
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
    /// Is this the locally-controlled player?
    /// </summary>
    private bool isLocal => playerId == Netplay.singleton.localPlayerId;

    [Header("Player control")]
    /// <summary>
    /// Current inputs of this player
    /// </summary>
    public PlayerInput input;

    /// <summary>
    /// Previous inputs of this player
    /// </summary>
    public PlayerInput lastInput;

    [Header("Team and identity")]
    [SyncVar(hook = nameof(OnColourChanged))]
    public Color colour;

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

    [Header("Ring drop")]
    public GameSound dropSound = new GameSound();

    public GameObject droppedRingPrefab;
    public Transform droppedRingSpawnPoint;

    public RingDropLayer[] ringDropLayers = new RingDropLayer[0];

    [Header("Hurt")]
    public float hurtDefaultHorizontalKnockback = 5;
    public float hurtDefaultVerticalKnockback = 5;

    // Components
    /// <summary>
    /// Player movement component
    /// </summary>
    [HideInInspector] public CharacterMovement movement;
    [HideInInspector] public Damageable damageable;

    public bool isInvisible { get; set; }

    public bool isHoldingFlag => holdingFlag != null;

    public TheFlag holdingFlag
    {
        get
        {
            if (NetGameState.singleton is NetGameStateCTF gameStateCTF && gameStateCTF.redFlag && gameStateCTF.blueFlag)
            {
                if (gameStateCTF.blueFlag.currentCarrier == playerId)
                    return gameStateCTF.blueFlag;
                if (gameStateCTF.redFlag.currentCarrier == playerId)
                    return gameStateCTF.redFlag;
            }
            return null;
        }
    }

    public float localTime = -1;

    void Awake()
    {
        movement = GetComponent<CharacterMovement>();
        damageable = GetComponent<Damageable>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        Netplay.singleton.RegisterPlayer(this);

        if (NetGameState.singleton is NetGameStateCTF netGameStateCTF)
            ChangeTeam(netGameStateCTF.FindBestTeamToJoin());

        Respawn();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Netplay.singleton.RegisterPlayer(this, playerId);
    }

    void Start()
    {
        PlayerControls control = new PlayerControls();

        damageable.onLocalDamaged.AddListener(OnDamaged);
    }

    void Update()
    {
        // Receive inputs if we're local
        if (isLocal)
        {
            lastInput = input;
            input = PlayerInput.MakeLocalInput(lastInput, GetComponent<CharacterMovement>().up);
        }

        if (isInvisible)
            characterModel.enabled = false;
        else if (!damageable.isInvincible) // blinking also controls visibility so we won't change it while invincible
            characterModel.enabled = true;

        foreach (Vector3 pos in ringDespawnedPositions)
        {
            DebugExtension.DebugPoint(pos, Color.green, 0.25f, 0.1f);
        }
        foreach (Vector3 pos in serverRingDespawnedPositions)
        {
            DebugExtension.DebugPoint(pos, Color.blue, 0.25f, 0.1f);
        }
    }

    // debugging to test accuracy of ring throw timing
    private List<Vector3> ringDespawnedPositions = new List<Vector3>();
    private List<Vector3> serverRingDespawnedPositions = new List<Vector3>();
    public int maxNumRingDespawnedPositions = 5;

    [ClientRpc]
    public void RpcNotifyRingDespawnedAt(Vector3 position)
    {
        ringDespawnedPositions.Add(position);
        if (ringDespawnedPositions.Count > maxNumRingDespawnedPositions)
            ringDespawnedPositions.RemoveAt(0);
    }
    public void LocalNotifyRingDespawnedAt(Vector3 position)
    {
        serverRingDespawnedPositions.Add(position);
        if (serverRingDespawnedPositions.Count > maxNumRingDespawnedPositions)
            serverRingDespawnedPositions.RemoveAt(0);
    }

    private static int nextSpawner = 0;

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

                TargetRespawn(spawnPoint.transform.forward);
            }
            else
            {
                Log.WriteWarning("No player spawners compatible with this team in this stage!");
            }
        }
    }

    [ClientRpc(channel = Channels.DefaultUnreliable)]
    private void TargetRespawn(Vector3 direction)
    {
        input.aimDirection = direction.Horizontal().normalized;
    }

    private void OnDamaged(GameObject instigator, Vector3 force, bool instaKill)
    {
        if (instaKill)
        {
            GetComponent<PlayerController>().CallEvent((Movement movement, bool isReconciliation) => 
            {
                if (!isReconciliation)
                    Respawn(); 
            });
        }
        else
        {
            if (force.sqrMagnitude <= Mathf.Epsilon)
                force = -transform.forward.Horizontal() * hurtDefaultHorizontalKnockback;
            else if (force.Horizontal().magnitude < hurtDefaultHorizontalKnockback)
                force.SetHorizontal(force.Horizontal().normalized * hurtDefaultHorizontalKnockback);

            // predict our hit
            GetComponent<PlayerController>().CallEvent((Movement movement, bool _) => (movement as CharacterMovement).ApplyHitKnockback(force + (movement as CharacterMovement).up * hurtDefaultVerticalKnockback));

            // only the server can do the rest (ring drop, score, etc)
            if (NetworkServer.active)
            {
                if (instigator && instigator.TryGetComponent(out Player attackerAsPlayer))
                {
                    // Give score to the attacker if possible
                    if (attackerAsPlayer)
                        attackerAsPlayer.score += 50;

                    // Add the hit message
                    MessageFeed.Post($"<player>{attackerAsPlayer.playerName}</player> hit <player>{playerName}</player>!");
                }

                // Drop some rings
                DropRings();
            }
        }

        if (NetworkServer.active)
        {
            if (NetGameState.singleton is NetGameStateCTF gameStateCTF)
            {
                if (holdingFlag)
                {
                    holdingFlag.Drop();
                }
            }
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
            if (ringShooting.weapons.Count > 1 && ringShooting.weapons[1].weaponType.droppedRingPrefab)
            {
                GameObject droppedWeapon = Spawner.Spawn(ringShooting.weapons[1].weaponType.droppedRingPrefab, droppedRingSpawnPoint.position, Quaternion.identity);

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
            GameSounds.PlaySound(gameObject, dropSound);

        numRings = 0;
    }

    private static readonly string[] nameSuffices = { " and Knuckles", " Jr", " Sr", " Classic", " Modern", " Esquire", " Ph.d", " Squared" }; // ive done my best

    public void Rename(string newName)
    {
        string updatedName = newName;
        int currentSuffix = 0;

        if (string.IsNullOrWhiteSpace(updatedName))
            updatedName = "Anonymous";

        while (Netplay.singleton.players.Exists(a => a != null && a != this && a.playerName == updatedName))
        {
            if (currentSuffix < nameSuffices.Length)
                updatedName = newName + nameSuffices[currentSuffix++];
            else
                updatedName = newName + nameSuffices[currentSuffix / nameSuffices.Length] + nameSuffices[currentSuffix % nameSuffices.Length]; // crap lol itll be fine ok
        }

        playerName = updatedName;
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
        characterModel.material.color = colour;
    }

    private void OnPlayerNameChanged(string oldVal, string newVal)
    {
        playerName = newVal;
        gameObject.name = playerName;
    }

    private void OnPlayerIdChanged(int oldVal, int newVal)
    {
        playerId = newVal;

        Netplay.singleton.RegisterPlayer(this, newVal);
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