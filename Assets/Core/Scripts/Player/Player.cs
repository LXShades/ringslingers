using Mirror;
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
    [SyncVar] public int playerId;

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

    public float localTime = -1;

    void Awake()
    {
        movement = GetComponent<CharacterMovement>();
        damageable = GetComponent<Damageable>();
    }

    void Start()
    {
        PlayerControls control = new PlayerControls();

        damageable.onLocalDamaged.AddListener(OnDamaged);
        Netplay.singleton.players[playerId] = this;

        if (isLocal)
        {
            Netplay.singleton.localPlayerId = playerId;
        }
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
    }

    public void Respawn()
    {
        GameObject[] spawners = GameObject.FindGameObjectsWithTag("PlayerSpawn");

        if (spawners.Length > 0)
        {
            GameObject spawnPoint = spawners[Random.Range(0, spawners.Length)];

            transform.position = spawnPoint.transform.position;
            transform.forward = spawnPoint.transform.forward.Horizontal(); // todo
        }
        else
        {
            Log.WriteWarning("No player spawners in this stage!");
        }
    }

    private void OnDamaged(GameObject instigator, Vector3 force)
    {
        // Only the server can really hurt us
        Hurt(instigator, force);
    }

    private void Hurt(GameObject instigator, Vector3 force)
    {
        if (force.sqrMagnitude <= Mathf.Epsilon)
            force = -transform.forward.Horizontal() * hurtDefaultHorizontalKnockback;
        else if (force.Horizontal().magnitude < hurtDefaultHorizontalKnockback)
            force.SetHorizontal(force.Horizontal().normalized * hurtDefaultHorizontalKnockback);

        // predict our hit
        GetComponent<PlayerController>().CallEvent((Movement movement, bool _) => (movement as CharacterMovement).ApplyHitKnockback(force + new Vector3(0, hurtDefaultVerticalKnockback, 0)));

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
                Movement ringMovement = Spawner.Spawn(droppedRingPrefab, droppedRingSpawnPoint.position, Quaternion.identity).GetComponent<Movement>();
                Ring ring = ringMovement?.GetComponent<Ring>();

                Debug.Assert(ringMovement && ring);

                ringMovement.velocity = new Vector3(Mathf.Sin(horizontalAngle) * horizontalVelocity, verticalVelocity, Mathf.Cos(horizontalAngle) * horizontalVelocity);
                ring.isDroppedRing = true;

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

        while (System.Array.Exists(Netplay.singleton.players, a => a != null && a != this && a.playerName == updatedName))
        {
            if (currentSuffix < nameSuffices.Length)
                updatedName = newName + nameSuffices[currentSuffix++];
            else
                updatedName = newName + nameSuffices[currentSuffix / nameSuffices.Length] + nameSuffices[currentSuffix % nameSuffices.Length]; // crap lol itll be fine ok
        }

        playerName = updatedName;
    }

    private void OnPlayerNameChanged(string oldVal, string newVal)
    {
        playerName = newVal;
        gameObject.name = playerName;
    }
}
