﻿using UnityEngine;

public class ThrownRingRail : ThrownRing
{
    public float maxRange = 100;

    public Transform endPoint;

    public LayerMask collisionLayers;

    public GameObject particles;

    public override void Simulate(float deltaTime)
    {
        return; // do nothing
    }

    public override void Throw(Player owner, Vector3 spawnPosition, Vector3 direction)
    {
        this.owner = owner.gameObject;
        transform.forward = direction;
        transform.position = spawnPosition;

        // Shoot the rail real far like
        float closestDistance = maxRange;
        Damageable closestDamageable = null;
        endPoint.transform.position = spawnPosition + direction * maxRange;
        endPoint.transform.up = -direction;

        RaycastHit[] hits = new RaycastHit[10];
        int numHits = Physics.RaycastNonAlloc(spawnPosition, direction, hits, maxRange, collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < numHits; i++)
        {
            Damageable damageable = hits[i].collider.GetComponentInParent<Damageable>();
            if (damageable && damageable.gameObject != owner.gameObject && hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closestDamageable = damageable;
                endPoint.transform.position = hits[i].point;
            }
        }

        if (Mirror.NetworkServer.active)
        {
            if (closestDamageable)
                closestDamageable.TryDamage(owner.gameObject, direction);
        }
    }
}
