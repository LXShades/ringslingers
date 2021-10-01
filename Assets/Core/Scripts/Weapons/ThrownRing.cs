using Mirror;
using UnityEngine;

public class ThrownRing : NetworkBehaviour
{
    private Vector3 spinAxis;

    protected RingWeaponSettings effectiveSettings = new RingWeaponSettings();

    [SyncVar]
    private Vector3 velocity;

    public GameObject owner
    {
        get => _owner;
        protected set
        {
            OnOwnerChanged(_owner, value);
            _owner = value;
        }
    }
    [SyncVar(hook = nameof(OnOwnerChanged))]
    private GameObject _owner;
    private Rigidbody rb;
    private Collider collider;

    private float spawnTime;

    private int currentNumWallSlides = 0;

    private bool isDead = false;

    private bool wasLocallyThrown = false;

    void Awake()
    {
        collider = GetComponentInChildren<Collider>();
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

        switch (effectiveSettings.projectileSpinAxisType)
        {
            case RingWeaponSettings.SpinAxisType.Wobble:
                spinAxis = Vector3.up + Vector3.right * Random.Range(-axisWobble, axisWobble) + Vector3.forward * Random.Range(-axisWobble, axisWobble);
                spinAxis.Normalize();
                break;
            case RingWeaponSettings.SpinAxisType.Forward:
                spinAxis = velocity.normalized;
                break;
            case RingWeaponSettings.SpinAxisType.Up:
                spinAxis = Vector3.up;
                break;
        }

        if (!NetworkServer.active && !wasLocallyThrown)
        {
            // shoot further ahead
            Simulate(GameTicker.singleton.localPlayerPing, true);
        }

        // colour the ring
        if (owner.TryGetComponent(out Character owningPlayer))
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

        GameSounds.PlaySound(transform.position, effectiveSettings.fireSound);
    }

    public override void OnStartClient()
    {
        RingWeaponSettings ownerEffectiveSettings = owner?.GetComponent<CharacterShooting>()?.effectiveWeaponSettings;

        if (ownerEffectiveSettings != null)
            effectiveSettings = ownerEffectiveSettings;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in renderer.materials)
                material.SetFloat("_RotationSpeed", effectiveSettings.projectileSpinSpeed);
        }
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (Time.time - spawnTime >= Time.fixedDeltaTime) // don't spawn backwards
        {
            // ditto, interpolation station
            transform.position -= velocity * Time.fixedDeltaTime;
        }
    }

    public virtual void Simulate(float deltaTime, bool isFirstSimulation = false)
    {
        if (isFirstSimulation)
        {
            // Simulate wall slides quickly
            Vector3 step = velocity * deltaTime;

            for (int i = 0; i < 3 && !isDead; i++)
            {
                if (step.magnitude < 0.01f)
                    break;

                if (rb.SweepTest(step.normalized, out RaycastHit collision, step.magnitude, QueryTriggerInteraction.Ignore))
                {
                    transform.position += step.normalized * (collision.distance - 0.1f);
                    HandleCollision(collider, collision.collider, collision.normal);

                    step = velocity.normalized * (step.magnitude - Mathf.Max(collision.distance - 0.01f, 0f)); // velocity might have changed so factor that in
                }
                else
                {
                    // no collisions, yay
                    transform.position += step;
                    break;
                }
            }
            Physics.SyncTransforms();
        }
        else
        {
            // using interpolation this shouldn't be needed
            transform.position += velocity * deltaTime;
        }

        rb.velocity = velocity;

        // Despawn on proximity
        if (effectiveSettings.proximityDespawnTriggerRange > 0f && velocity.sqrMagnitude <= 1f) // kinda hack, grenades
        {
            Vector3 myPosition = transform.position;
            foreach (Character character in Netplay.singleton.players)
            {
                if (character && character.gameObject != owner && Vector3.Distance(character.transform.position, myPosition) < effectiveSettings.proximityDespawnTriggerRange)
                {
                    Despawn();
                    return;
                }
            }
        }
    }

    public virtual void Throw(Character owner, Vector3 spawnPosition, Vector3 direction)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, ownerCollider);
        }

        this.owner = owner.gameObject;
        this.wasLocallyThrown = true;

        velocity = direction.normalized * effectiveSettings.projectileSpeed;

        rb.MovePosition(spawnPosition);
        transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(direction));
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collider, collision.collider, collision.contacts[0].normal);
    }

    private void HandleCollision(Collider myCollider, Collider otherCollider, Vector3 normal)
    {
        if (isDead)
            return; // Unity physics bugs are pain
                    //if (Time.time - spawnTime < 0.1f)
                    //return; // HACK: prevent destroying self before syncvars, etc are ready (this can happen...)
        if (otherCollider.gameObject == owner)
            return; // don't collide with the player who threw the ring

        // Hurt any players we collided with
        if (otherCollider.TryGetComponent(out Damageable damageable) && owner)
        {
            if (damageable.gameObject == owner)
                return; // actually we're fine here

            damageable.TryDamage(owner, velocity.normalized * effectiveSettings.projectileKnockback);
        }

        // Ignore collisions with other rings
        if (otherCollider.TryGetComponent(out ThrownRing thrownRing))
        {
            if (thrownRing.owner == owner)
                return; // don't collide with our other rings
        }

        // Do wall slides, if allowed
        if (currentNumWallSlides < effectiveSettings.numWallSlides)
        {
            velocity = velocity.AlongPlane(normal).normalized * velocity.magnitude + normal * 0.01f;

            if (Physics.ComputePenetration(myCollider, myCollider.transform.position, myCollider.transform.rotation,
                otherCollider, otherCollider.transform.position, otherCollider.transform.rotation, out Vector3 depenetrationDir, out float depenetrationDistance))
            {
                transform.position += depenetrationDir * depenetrationDistance;
            }

            currentNumWallSlides++;
            return;
        }

        if (effectiveSettings.contactAction == RingWeaponSettings.ContactAction.Despawn)
        {
            Despawn();
        }
        else if (effectiveSettings.contactAction == RingWeaponSettings.ContactAction.Stop)
        {
            // Don't despawn, just stop
            rb.velocity = Vector3.zero;
            velocity = Vector3.zero;

            if (Physics.ComputePenetration(myCollider, myCollider.transform.position, myCollider.transform.rotation,
                otherCollider, otherCollider.transform.position, otherCollider.transform.rotation, out Vector3 depenetrationDir, out float depenetrationDistance))
            {
                transform.position += depenetrationDir * depenetrationDistance;
            }
        }
    }

    private void Despawn()
    {
        // Play despawn sound
        GameSounds.PlaySound(gameObject, effectiveSettings.despawnSound);

        isDead = true;

        SpawnContactEffect(transform.position);

        Spawner.Despawn(gameObject);
    }

    protected void SpawnContactEffect(Vector3 position)
    {
        if (effectiveSettings.contactEffect && owner)
        {
            GameObject obj = Spawner.Spawn(effectiveSettings.contactEffect, position, Quaternion.identity);

            if (obj.TryGetComponent(out DamageOnTouch damager))
            {
                damager.owner = owner;
                damager.team = owner.GetComponent<Damageable>().damageTeam;
                damager.knockback = effectiveSettings.projectileKnockback;
            }
        }
    }
    
    // when prediction is successful, we get teleported back a bit and resimulate forward again
    private void OnPredictionSuccessful()
    {
        Simulate(GameTicker.singleton.localPlayerPing);
    }

    private void OnOwnerChanged(GameObject oldOwner, GameObject newOwner)
    {
        if (newOwner.TryGetComponent(out CharacterShooting ringShooting))
        {
            effectiveSettings = ringShooting.effectiveWeaponSettings;
        }
    }
}
