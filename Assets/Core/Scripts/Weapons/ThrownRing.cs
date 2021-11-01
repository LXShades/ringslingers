using Mirror;
using UnityEngine;

public class ThrownRing : NetworkBehaviour
{
    protected RingWeaponSettings effectiveSettings = new RingWeaponSettings();

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
    [SyncVar]
    private float serverTimeAtSpawn;
    [SyncVar]
    private Vector3 initialVelocity;

    private Movement movement;
    private HopTrails hopTrails;

    private int currentNumWallSlides = 0;

    private bool wasLocallyThrown = false;

    void Awake()
    {
        movement = GetComponent<Movement>();
        hopTrails = GetComponentInChildren<HopTrails>();

        if (TryGetComponent(out Predictable predictable))
        {
            predictable.onPredictionSuccessful += OnPredictionSuccessful;
        }
    }

    void Start()
    {
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
        
        if (movement) // rail rings don't have movement
            movement.gameObjectInstancesToIgnore.Add(owner);

        velocity = initialVelocity;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in renderer.materials)
                material.SetFloat("_RotationSpeed", effectiveSettings.projectileSpinSpeed);
        }

        if (!NetworkServer.active && !wasLocallyThrown)
        {
            Vector3 originalPosition = transform.position;

            // shoot further ahead
            Simulate(GameTicker.singleton.predictedServerTime - serverTimeAtSpawn, true);

            if (hopTrails)
                hopTrails.AddTrail(originalPosition, transform.position);
        }
    }

    public override void OnStartServer()
    {
        serverTimeAtSpawn = GameTicker.singleton.predictedServerTime;
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
    }

    public virtual void Simulate(float deltaTime, bool isFirstSimulation = false)
    {
        if (movement)
        {
            // Move and check collisions
            bool wasHit = movement.Move(velocity * deltaTime, out Movement.Hit hit);

            if (wasHit)
            {
                HandleCollision(hit.collider, hit.normal);

                if (this == null) // we got destroyed in the process
                    return;
            }
        }

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
        initialVelocity = velocity;

        if (movement)
            movement.gameObjectInstancesToIgnore.Add(owner.gameObject);

        transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(direction));
    }

    private void HandleCollision(Collider otherCollider, Vector3 normal)
    {
        // Ignore collisions with other rings
        if (otherCollider.TryGetComponent(out ThrownRing thrownRing))
        {
            if (thrownRing.owner == owner)
                return; // don't collide with our other rings
        }

        // Hurt any players we collided with
        if (otherCollider.TryGetComponent(out Damageable damageable) && owner)
            damageable.TryDamage(owner, velocity.normalized * effectiveSettings.projectileKnockback);
        // Do wall slides, if allowed - but not against other players
        else if (currentNumWallSlides < effectiveSettings.numWallSlides)
        {
            Debug.Log($"Bounce pre {velocity}");
            float originalVelocityMagnitude = velocity.magnitude;
            velocity.SetAlongAxis(normal, 0.01f /* push away slightly */);
            velocity *= originalVelocityMagnitude / velocity.magnitude;

            Debug.Log($"Bounce post {velocity}");
            currentNumWallSlides++;
            return;
        }

        if (effectiveSettings.contactAction == RingWeaponSettings.ContactAction.Despawn)
        {
            Debug.Log($"Death {velocity}");
            Despawn();
        }
        else if (effectiveSettings.contactAction == RingWeaponSettings.ContactAction.Stop)
        {
            // Don't despawn, just stop
            velocity = Vector3.zero;

            if (isServer)
                Stop(transform.position);
        }
    }

    // RPC'd so that grenades always land in the same place
    [ClientRpc(channel = Channels.Unreliable)]
    private void Stop(Vector3 restingPosition)
    {
        velocity = Vector3.zero;
        transform.position = restingPosition;
    }

    private void Despawn()
    {
        // Play despawn sound
        GameSounds.PlaySound(gameObject, effectiveSettings.despawnSound);

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
        float jumpAheadSimulation = GameTicker.singleton.predictedServerTime - serverTimeAtSpawn;

        // We need to reset our state as well
        currentNumWallSlides = 0;

        if (jumpAheadSimulation > 0f)
            Simulate(jumpAheadSimulation);
    }

    private void OnOwnerChanged(GameObject oldOwner, GameObject newOwner)
    {
        if (newOwner.TryGetComponent(out CharacterShooting ringShooting))
        {
            effectiveSettings = ringShooting.effectiveWeaponSettings;
        }
    }
}
