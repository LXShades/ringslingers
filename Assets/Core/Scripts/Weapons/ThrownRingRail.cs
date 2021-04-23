using UnityEngine;

public class ThrownRingRail : ThrownRing
{
    public float maxRange = 100;

    public Transform endPoint;

    public LayerMask collisionLayers;

    public GameObject particles;

    private bool endPointWasSet = false;

    public override void Simulate(float deltaTime)
    {
        if (!endPointWasSet)
        {
            // clients need to do this because Throw doesn't get called
            RaycastHit hit;

            if (Physics.Raycast(transform.position, transform.forward, out hit, maxRange, collisionLayers, QueryTriggerInteraction.Ignore))
                endPoint.transform.position = hit.point;
            else
                endPoint.transform.position = transform.position + transform.forward * maxRange;

            endPointWasSet = true;
        }

        return; // do nothing else
    }

    public override void Throw(Character owner, Vector3 spawnPosition, Vector3 direction)
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
            if (hits[i].distance < closestDistance)
            {
                Damageable damageable = hits[i].collider.GetComponentInParent<Damageable>();
                if (damageable && damageable.gameObject != owner.gameObject)
                    closestDamageable = damageable;
                else
                    closestDamageable = null;

                endPoint.transform.position = hits[i].point;
                closestDistance = hits[i].distance;
            }
        }

        if (closestDamageable)
            closestDamageable.TryDamage(owner.gameObject, direction);

        GameSounds.PlaySound(endPoint.transform.position, effectiveSettings.despawnSound);
        SpawnContactEffect(endPoint.transform.position);

        endPointWasSet = true;
    }
}
