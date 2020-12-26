using UnityEngine;

public class Ring : MonoBehaviour
{
    [Header("Ring")]
    [Tooltip("Speed that this ring spins at, in degrees per second")] public float spinSpeed = 180;
    public float hoverHeight = 0.375f;

    [Header("Dropped rings")]
    public bool isDroppedRing = false;

    public float pickupWarmupDuration = 0.75f;

    [Header("Hierarchy")]
    public GameObject pickupParticles;

    public GameSound pickupSound = new GameSound();

    // Components
    private RespawnableItem respawnableItem;

    public delegate void OnPickup(Player player);
    public OnPickup onPickup;

    private float awakeTime;

    void Awake()
    {
        respawnableItem = GetComponent<RespawnableItem>();
        awakeTime = Time.unscaledTime;
    }

    void Start()
    {
        // Hover above the ground
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -Vector3.up, out hit, hoverHeight, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + new Vector3(0, hoverHeight, 0);
        }
    }

    void Update()
    {
        if (isDroppedRing || respawnableItem.isSpawned)
        {
            // Spinny spin
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(0, spinSpeed * Time.deltaTime, 0));
        }
    }

    private void OnTriggerStay(Collider other)
    {
        Player otherPlayer = other.GetComponent<Player>();
        if (otherPlayer && (!isDroppedRing || Time.unscaledTime - awakeTime >= pickupWarmupDuration))
        {
            otherPlayer.numRings++;
            pickupParticles.SetActive(true);
            pickupParticles.transform.SetParent(null);

            GameSounds.PlaySound(other.gameObject, pickupSound);

            if (!isDroppedRing)
                respawnableItem.Pickup();
            else
                Spawner.Despawn(gameObject); // we aren't going to respawn here

            onPickup?.Invoke(otherPlayer);
        }
    }
}
