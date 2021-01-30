using Mirror;
using UnityEngine;

public class Ring : NetworkBehaviour, ISpawnCallbacks
{
    [Tooltip("Speed that this ring spins at, in degrees per second")] public float spinSpeed = 180;
    public float hoverHeight = 0.375f;

    [Header("Dropped rings")]
    [SyncVar]
    public bool isDroppedRing = false;

    public float pickupWarmupDuration = 0.75f;

    [Header("Hierarchy")]
    public GameObject pickupParticles;

    public GameSound pickupSound = new GameSound();

    // Components
    private RespawnableItem respawnableItem;
    private DespawnAfterDuration despawn;
    private SyncMovement syncMovement;
    private Movement movement;

    public delegate void OnPickup(Player player);
    public OnPickup onPickup;

    private Player probablePickedUpPlayer;

    private float awakeTime;

    private bool canPickup = true;

    void Awake()
    {
        respawnableItem = GetComponent<RespawnableItem>();
        despawn = GetComponent<DespawnAfterDuration>();
        syncMovement = GetComponent<SyncMovement>();
        movement = GetComponent<Movement>();

        if (respawnableItem)
        {
            respawnableItem.onRespawn += OnRespawn;
            respawnableItem.onDespawn += OnPickedUp;
        }

        awakeTime = Time.unscaledTime;
    }

    void Start()
    {
        // dropped rings and level item rings behave quite differently, but can share the same prefab
        // meaning we can make a variant for weapon rings etc without making things super complex
        despawn.enabled = isDroppedRing;
        syncMovement.enabled = isDroppedRing;
        movement.enabled = isDroppedRing;
        respawnableItem.enabled = !isDroppedRing;

        transform.rotation = Quaternion.Euler(new Vector3(0, transform.rotation.eulerAngles.y, 0)); // always start upright

        // Hover above the ground
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, -Vector3.up, out hit, hoverHeight, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + new Vector3(0, hoverHeight, 0);
        }

        if (!isDroppedRing && respawnableItem)
            respawnableItem.SetSpawnPosition(transform.position);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        /*despawn.enabled = isDroppedRing;
        syncMovement.enabled = isDroppedRing;
        movement.enabled = isDroppedRing;
        respawnableItem.enabled = !isDroppedRing;*/
    }

    void Update()
    {
        if (isDroppedRing || respawnableItem.isSpawned)
        {
            // Spinny spin
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(0, spinSpeed * Time.deltaTime, 0));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Player otherPlayer = other.GetComponent<Player>();
        if (otherPlayer && (!isDroppedRing || Time.unscaledTime - awakeTime >= pickupWarmupDuration) && canPickup)
        {
            probablePickedUpPlayer = otherPlayer;

            if (NetworkServer.active)
            {
                otherPlayer.numRings++;

                if (!isDroppedRing)
                    respawnableItem.Despawn();
                else
                    Spawner.Despawn(gameObject); // we aren't going to respawn here

                canPickup = false; // HACK: collider calls can happen even after despawning...WHY?? (oh maybe because two colliders on the player... ohhhhh)

                onPickup?.Invoke(otherPlayer);
            }
        }
    }

    private void OnPickedUp()
    {
        PlayPickupEffects();
    }

    public void OnBeforeDespawn()
    {
        PlayPickupEffects();
    }

    private void PlayPickupEffects()
    {
        if (probablePickedUpPlayer)
        {
            pickupParticles.SetActive(true);
            pickupParticles.transform.SetParent(null);

            GameSounds.PlaySound(probablePickedUpPlayer != null && probablePickedUpPlayer.playerId == Netplay.singleton.localPlayerId ? probablePickedUpPlayer.gameObject : gameObject, pickupSound);
        }
    }

    private void OnRespawn()
    {
        probablePickedUpPlayer = null;
        canPickup = true;
    }
}
