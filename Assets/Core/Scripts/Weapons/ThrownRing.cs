﻿using UnityEngine;

public class ThrownRing : WorldObjectComponent
{
    [HideInInspector] public RingWeaponSettings settings;

    private Vector3 spinAxis;

    private Vector3 velocity;

    protected Player owner;
    private Rigidbody rb;

    public override void WorldAwake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void WorldStart()
    {
        float axisWobble = 0.5f;

        spinAxis = Vector3.up + Vector3.right * Random.Range(-axisWobble, axisWobble) + Vector3.forward * Random.Range(-axisWobble, axisWobble);
        spinAxis.Normalize();

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RingWeaponSettings>(); // better than nothing?
        }
    }

    public override void WorldUpdate(float deltaTime)
    {
        // Spin
        transform.rotation *= Quaternion.AngleAxis(settings.projectileSpinSpeed * deltaTime, spinAxis);

        // Move manually cuz... uh well, hmm, dangit rigidbody interpolation is not a thing in manually-simulated physics
        transform.position += velocity * deltaTime;

        // Improves collisions, kinda annoying but it be that way
        rb.velocity = velocity;
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Play despawn sound
        GameSounds.PlaySound(gameObject, settings.despawnSound);

        // Hurt any players we collided with
        Player otherPlayer = collision.collider.GetComponent<Player>();
        if (otherPlayer)
        {
            if (otherPlayer == owner)
                return; // actually we're fine here

            otherPlayer.Hurt(owner.gameObject);
        }

        // Bye-bye!
        World.Despawn(gameObject);
    }

    public virtual void Throw(Player owner, Vector3 spawnPosition, Vector3 direction, float jumpAheadTime = 0f)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, ownerCollider);
        }

        this.owner = owner;
        velocity = direction.normalized * settings.projectileSpeed;
        transform.position = spawnPosition;

        transform.position += jumpAheadTime * velocity;
    }
}
