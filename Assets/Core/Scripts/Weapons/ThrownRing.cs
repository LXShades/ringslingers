using Mirror;
using UnityEngine;

public class ThrownRing : NetworkBehaviour
{
    private Vector3 spinAxis;

    private RingWeaponSettings effectiveSettings = new RingWeaponSettings();

    [SyncVar]
    private Vector3 velocity;

    public GameObject owner => _owner;
    [SyncVar(hook = nameof(OnOwnerChanged))]
    protected GameObject _owner;
    private Rigidbody rb;

    private float spawnTime;

    private bool isDead = false;

    private bool wasLocallyThrown = false;

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
            Simulate(PlayerTicker.singleton.localPlayerPing);
        }

        // colour the ring
        if (_owner.TryGetComponent(out Player owningPlayer))
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

    public override void OnStartClient()
    {
        RingWeaponSettings ownerEffectiveSettings = owner?.GetComponent<RingShooting>()?.effectiveWeaponSettings;

        if (ownerEffectiveSettings != null)
            effectiveSettings = ownerEffectiveSettings;
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
    }

    public virtual void Simulate(float deltaTime)
    {
        // Spin
        transform.rotation *= Quaternion.AngleAxis(effectiveSettings.projectileSpinSpeed * deltaTime, spinAxis);

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

        this._owner = owner.gameObject;
        this.effectiveSettings = owner.GetComponent<RingShooting>().effectiveWeaponSettings;
        this.wasLocallyThrown = true;

        velocity = direction.normalized * effectiveSettings.projectileSpeed;
        transform.position = spawnPosition;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead)
            return; // Unity physics bugs are pain
        //if (Time.time - spawnTime < 0.1f)
            //return; // HACK: prevent destroying self before syncvars, etc are ready (this can happen...)
        if (collision.collider.gameObject == _owner)
            return; // don't collide with the player who threw the ring

        // Hurt any players we collided with
        if (collision.collider.TryGetComponent(out Damageable damageable) && _owner)
        {
            if (damageable.gameObject == _owner)
                return; // actually we're fine here

            damageable.TryDamage(_owner, velocity.normalized * effectiveSettings.projectileKnockback);
        }

        if (collision.collider.TryGetComponent(out ThrownRing thrownRing))
        {
            if (thrownRing._owner == _owner)
                return; // don't collide with our other rings
        }

        // Play despawn sound
        GameSounds.PlaySound(gameObject, effectiveSettings.despawnSound);

        isDead = true;

        SpawnContactEffect(transform.position);

        Spawner.Despawn(gameObject);
    }

    protected void SpawnContactEffect(Vector3 position)
    {
        if (effectiveSettings.contactEffect && _owner)
        {
            GameObject obj = Spawner.Spawn(effectiveSettings.contactEffect, position, Quaternion.identity);

            if (obj.TryGetComponent(out DamageOnTouch damager))
            {
                damager.owner = _owner;
                damager.team = _owner.GetComponent<Damageable>().damageTeam;
            }
        }
    }
    
    // when prediction is successful, we get teleported back a bit and resimulate forward again
    private void OnPredictionSuccessful()
    {
        Simulate(Time.time - spawnTime);
    }

    private void OnOwnerChanged(GameObject oldOwner, GameObject newOwner)
    {
        if (newOwner.TryGetComponent(out RingShooting ringShooting))
        {
            effectiveSettings = ringShooting.effectiveWeaponSettings;
        }
    }
}
