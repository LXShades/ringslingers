using Mirror;
using UnityEngine;

public class ThrownRing : NetworkBehaviour, IPredictableObject
{
    [HideInInspector] public RingWeaponSettings settings;

    private Vector3 spinAxis;

    [SyncVar]
    private Vector3 velocity;

    [SyncVar]
    protected GameObject owner;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        float axisWobble = 0.5f;

        spinAxis = Vector3.up + Vector3.right * Random.Range(-axisWobble, axisWobble) + Vector3.forward * Random.Range(-axisWobble, axisWobble);
        spinAxis.Normalize();

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RingWeaponSettings>(); // better than nothing?
        }
    }

    public virtual void Update()
    {
        // Spin
        transform.rotation *= Quaternion.AngleAxis(settings.projectileSpinSpeed * Time.deltaTime, spinAxis);

        // Move manually cuz... uh well, hmm, dangit rigidbody interpolation is not a thing in manually-simulated physics
        transform.position += velocity * Time.deltaTime;

        // Improves collisions, kinda annoying but it be that way
        rb.velocity = velocity;
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Play despawn sound
        GameSounds.PlaySound(gameObject, settings.despawnSound);

        // Hurt any players we collided with
        if (collision.collider.TryGetComponent(out Player otherPlayer) && owner)
        {
            if (otherPlayer.gameObject == owner)
                return; // actually we're fine here

            otherPlayer.Hurt(owner);
        }

        if (collision.collider.TryGetComponent(out ThrownRing thrownRing))
        {
            if (thrownRing.owner == owner)
                return; // don't collider with our other rings
        }

        Destroy(gameObject);
    }

    public virtual void Throw(Player owner, Vector3 spawnPosition, Vector3 direction, float jumpAheadTime = 0f)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, ownerCollider);
        }

        this.owner = owner.gameObject;
        velocity = direction.normalized * settings.projectileSpeed;
        transform.position = spawnPosition;

        transform.position += jumpAheadTime * velocity;
    }
}
