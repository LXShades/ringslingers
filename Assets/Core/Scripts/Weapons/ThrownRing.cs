using Mirror;
using UnityEngine;

public class ThrownRing : NetworkBehaviour
{
    [HideInInspector] public RingWeaponSettings settings;

    private Vector3 spinAxis;

    [SyncVar]
    private Vector3 velocity;

    [SyncVar]
    protected GameObject owner;
    private Rigidbody rb;

    private float spawnTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnTime = Time.time;

        if (TryGetComponent(out Predictable predictable))
        {
            predictable.onPredictionSuccessful += OnPredictionSuccessful;
        }
    }

    void Start()
    {
        float axisWobble = 0.5f;

        spinAxis = Vector3.up + Vector3.right * Random.Range(-axisWobble, axisWobble) + Vector3.forward * Random.Range(-axisWobble, axisWobble);
        spinAxis.Normalize();

        if (settings == null)
        {
            Log.WriteWarning($"{gameObject}: Ring weapon settings is missing!?");
            settings = ScriptableObject.CreateInstance<RingWeaponSettings>(); // better than nothing?
        }

        if (NetworkClient.active && !NetworkServer.active && Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.gameObject != owner)
        {
            // shoot further ahead
            Simulate(Netplay.singleton.unreliablePing);
        }
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
    }

    public virtual void Simulate(float deltaTime)
    {
        // Spin
        transform.rotation *= Quaternion.AngleAxis(settings.projectileSpinSpeed * deltaTime, spinAxis);

        // Move manually cuz... uh well, hmm, dangit rigidbody interpolation is not a thing in manually-simulated physics
        transform.position += velocity * deltaTime;

        // Improves collisions, kinda annoying but it be that way
        rb.velocity = velocity;
    }

    public virtual void Throw(Player owner, Vector3 spawnPosition, Vector3 direction)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, ownerCollider);
        }

        this.owner = owner.gameObject;
        velocity = direction.normalized * settings.projectileSpeed;
        transform.position = spawnPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time - spawnTime < 0.1f)
            return; // HACK: prevent destroying self before syncvars, etc are ready (this can happen...)
        if (collision.collider.gameObject == owner)
            return; // don't collide with the player who threw the ring

        // Play despawn sound
        GameSounds.PlaySound(gameObject, settings.despawnSound);

        // Hurt any players we collided with
        if (collision.collider.TryGetComponent(out Damageable damageable) && owner)
        {
            if (damageable.gameObject == owner)
                return; // actually we're fine here

            damageable.TryDamage(owner, velocity.normalized * settings.projectileKnockback);
        }

        if (collision.collider.TryGetComponent(out ThrownRing thrownRing))
        {
            if (thrownRing.owner == owner)
                return; // don't collide with our other rings
        }

        Spawner.Despawn(gameObject);
    }

    private void OnPredictionSuccessful()
    {
        Simulate(Time.time - spawnTime);
    }
}
