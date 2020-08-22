using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrownRingRail : ThrownRing
{
    public float maxRange = 100;

    public Transform endPoint;

    public LayerMask collisionLayers;

    [WorldSharedReference] public GameObject particles;

    public override void WorldUpdate(float deltaTime)
    {
        return; // do nothing, instead of the default movement
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

        RaycastHit[] hits = new RaycastHit[10];
        int numHits = worldObject.world.physics.Raycast(spawnPosition, direction, hits, maxRange, collisionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < numHits; i++)
        {
            Player player = hits[i].collider.GetComponentInParent<Player>();
            if (player != owner && hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closestPlayer = player;
                endPoint.transform.position = hits[i].point;
            }
        }

        if (closestPlayer)
        {
            Debug.Log($"Trying to hurt player {closestPlayer}");
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
