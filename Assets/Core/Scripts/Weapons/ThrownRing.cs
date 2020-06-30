using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrownRing : SyncedObject
{
    [HideInInspector] public RingWeaponSettings settings;

    private Vector3 spinAxis;

    private Vector3 velocity;

    private float spawnTime = 0;

    private Player owner;
    private Rigidbody rb;

    public override void FrameAwake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void FrameStart()
    {
        float axisWobble = 0.5f;

        spinAxis = Vector3.up + Vector3.right * Random.Range(-axisWobble, axisWobble) + Vector3.forward * Random.Range(-axisWobble, axisWobble);
        spinAxis.Normalize();

        spawnTime = Frame.local.time;
    }

    public override void FrameUpdate()
    {
        // Spin
        transform.rotation *= Quaternion.AngleAxis(settings.projectileSpinSpeed * Frame.local.deltaTime, spinAxis);

        // Move
        //transform.position += velocity * Frame.local.deltaTime;

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
            otherPlayer.Hurt(owner.gameObject);
        }

        // Bye-bye!
        GameManager.DestroyObject(gameObject);
    }

    public void Throw(Player owner, Vector3 spawnPosition, Vector3 direction)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(collider, ownerCollider);
        }

        this.owner = owner;
        velocity = direction.normalized * settings.projectileSpeed;
        transform.position = spawnPosition;
    }
}
