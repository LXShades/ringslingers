using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class ThrownRingRail : ThrownRing
{
    public float maxRange = 100;

    public Transform endPoint;

    public LayerMask collisionLayers;

    public GameObject particles;

    private bool endPointWasSet = false;

    public override void Simulate(float deltaTime, bool isFirstSimulation)
    {
        if (!endPointWasSet)
        {
            // clients need to do this because Throw doesn't get called
            RaycastHit hit;

            if (Physics.Raycast(transform.position + transform.forward * 0.5f, transform.forward, out hit, maxRange, collisionLayers, QueryTriggerInteraction.Ignore))
                endPoint.transform.position = hit.point;
            else
                endPoint.transform.position = transform.position + transform.forward * maxRange;

            endPointWasSet = true;
        }

        return; // do nothing else
    }

    private List<PastCharacter> originalCharacterStates = new List<PastCharacter>();

    public override void Throw(Character owner, Vector3 spawnPosition, Vector3 direction, float serverPredictionAmount)
    {
        this.owner = owner.gameObject;

        transform.forward = direction;
        transform.position = spawnPosition;

        // Shoot the rail real far like
        float closestDistance = maxRange;
        Damageable closestDamageable = null;
        endPoint.transform.position = spawnPosition + direction * maxRange;
        endPoint.transform.up = -direction;

        // Run server rewinds
        originalCharacterStates.Clear();
        if (NetworkServer.active && serverPredictionAmount > 0f)
        {
            float serverTime = GameTicker.singleton.predictedServerTime;
            float angleWindowPerMetreDistanceRad = 50f * Mathf.Deg2Rad;

            direction.Normalize(); // so that the dot is accurate

            // candidate characters are rewound to the earlier state
            foreach (Character character in Netplay.singleton.players)
            {
                if (character && character != owner)
                {
                    int pastStateIndex = character.ticker.stateTimeline.ClosestIndexBeforeOrEarliest(serverTime - serverPredictionAmount);

                    if (pastStateIndex != -1)
                    {
                        if (Mathf.Acos(Vector3.Dot(direction, (character.ticker.stateTimeline[pastStateIndex].position - spawnPosition).normalized)) < angleWindowPerMetreDistanceRad / Vector3.Distance(spawnPosition, character.transform.position))
                        {
                            originalCharacterStates.Add(new PastCharacter() { character = character, originalState = character.MakeState() });
                            character.ApplyState(character.ticker.stateTimeline[pastStateIndex]);
                        }
                    }
                }
            }
        }

        // Run the hitscan against damageables
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

        // If server rewinds were used, restore the characters
        foreach (PastCharacter pastCharacter in originalCharacterStates)
            pastCharacter.character.ApplyState(pastCharacter.originalState);

        if (closestDamageable)
            closestDamageable.TryDamage(owner.gameObject, direction);

        GameSounds.PlaySound(endPoint.transform.position, effectiveSettings.despawnSound);
        SpawnContactEffect(endPoint.transform.position);

        endPointWasSet = true;
    }
}
