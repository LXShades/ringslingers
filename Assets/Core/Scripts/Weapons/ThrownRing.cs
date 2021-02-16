using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class ThrownRing : NetworkBehaviour
{
    public enum SpinAxisType
    {
        Up,
        Forward,
        Wobble
    }

    [SyncVar]
    [HideInInspector] public RingWeaponSettings settings;

    public SpinAxisType spinAxisType = SpinAxisType.Wobble;

    private Vector3 spinAxis;

    [SyncVar]
    private Vector3 velocity;

    [SyncVar]
    protected GameObject owner;
    private Rigidbody rb;

    private float spawnTime;

    private bool isDead = false;

    public UnityEvent onDespawn;

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

        switch (spinAxisType)
        {
            case SpinAxisType.Wobble:
                spinAxis = Vector3.up + Vector3.right * Random.Range(-axisWobble, axisWobble) + Vector3.forward * Random.Range(-axisWobble, axisWobble);
                spinAxis.Normalize();
                break;
            case SpinAxisType.Forward:
                spinAxis = velocity.normalized;
                break;
            case SpinAxisType.Up:
                spinAxis = Vector3.up;
                break;
        }

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

        // colour the ring
        if (owner.TryGetComponent(out Player owningPlayer))
        {
            switch (owningPlayer.team)
            {
                case PlayerTeam.Red:
                    GetComponentInChildren<Renderer>().material.color = new Color(1, 0, 0);
                    break;
                case PlayerTeam.Blue:
                    GetComponentInChildren<Renderer>().material.color = new Color(0, 0, 1);
                    break;
            }
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
        if (isDead)
            return; // Unity physics bugs are pain
        //if (Time.time - spawnTime < 0.1f)
            //return; // HACK: prevent destroying self before syncvars, etc are ready (this can happen...)
        if (collision.collider.gameObject == owner)
            return; // don't collide with the player who threw the ring

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

        // Play despawn sound
        GameSounds.PlaySound(gameObject, settings.despawnSound);

        if (owner && owner.TryGetComponent(out Player ownerPlayer))
        {
            if (NetworkServer.active)
                ownerPlayer.RpcNotifyRingDespawnedAt(transform.position);
            else
                ownerPlayer.LocalNotifyRingDespawnedAt(transform.position);
        }

        isDead = true;
        onDespawn?.Invoke();
        Spawner.Despawn(gameObject);
    }

    private void OnPredictionSuccessful()
    {
        Simulate(Time.time - spawnTime);
    }
}
