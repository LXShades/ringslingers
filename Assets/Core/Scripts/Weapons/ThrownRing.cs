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
        transform.position += velocity * Frame.local.deltaTime;

        // Despawn after a while
        if (Frame.local.time - spawnTime >= settings.projectileLifetime)
        {
            Destroy(gameObject);
        }
    }

    public void Throw(Player owner, Vector3 spawnPosition, Vector3 direction)
    {
        this.owner = owner;
        velocity = direction.normalized * settings.projectileSpeed;
        transform.position = spawnPosition;
    }
}
