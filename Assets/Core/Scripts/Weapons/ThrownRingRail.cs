using UnityEngine;

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
        Player closestPlayer = null;
        endPoint.transform.position = spawnPosition + direction * maxRange;
        endPoint.transform.up = -direction;

        RaycastHit[] hits = new RaycastHit[10];
        int numHits = Physics.RaycastNonAlloc(spawnPosition, direction, hits, maxRange, collisionLayers, QueryTriggerInteraction.Ignore);
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
            Log.Write($"Trying to hurt player {closestPlayer}");
            closestPlayer.Hurt(owner.gameObject);
        }
    }
}
