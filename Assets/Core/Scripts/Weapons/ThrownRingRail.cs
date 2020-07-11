using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrownRingRail : ThrownRing
{
    public float maxRange = 100;

    public Transform endPoint;

    public LayerMask collisionLayers;

    public GameObject particles;

    public override void FrameUpdate()
    {
        return; // do nothing
    }

    public override void Throw(Player owner, Vector3 spawnPosition, Vector3 direction)
    {
        this.owner = owner;
        transform.forward = direction;
        transform.position = spawnPosition;

        // Shoot the rail real far like
        float closestDistance = maxRange;
        Player closestPlayer = null;
        endPoint.transform.position = spawnPosition + direction * maxRange;
        endPoint.transform.up = -direction;

        foreach (RaycastHit hit in Physics.RaycastAll(spawnPosition, direction, maxRange, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            Player player = hit.collider.GetComponentInParent<Player>();
            if (player != owner && hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestPlayer = player;
                endPoint.transform.position = hit.point;
            }
        }

        if (closestPlayer)
        {
            closestPlayer.Hurt(owner.gameObject);
        }

        if (!World.live.isResimulation)
        {
            // release the particles so they won't be despawned
            particles.transform.SetParent(null, true);
            endPoint.transform.SetParent(null, true);
        }
    }
}
