using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ThrownRingRail : ThrownRing
{
    [Header("Collision")]
    public float maxRange = 100;
    public LayerMask collisionLayers;

    [Header("Effects")]
    public float tubeEffectScaleFactor = 2f;
    public Transform tubeEffect;
    public Transform endPoint;
    public GameObject particles;

    private bool endPointWasSet = false;

    RaycastHit[] hits = new RaycastHit[10];

    public override void Simulate(float deltaTime, bool isFirstSimulation)
    {
        if (!endPointWasSet)
        {
            Vector3 forward = transform.forward;
            Vector3 position = transform.position;
            // clients need to do this because Throw doesn't get called
            RaycastHit hit;

            if (Physics.Raycast(position + forward * 0.5f, forward, out hit, maxRange, collisionLayers, QueryTriggerInteraction.Ignore))
                endPoint.transform.position = hit.point;
            else
                endPoint.transform.position = position + forward * maxRange;

            InitVfx();
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
            double serverTime = GameTicker.singleton.predictedServerTime;
            float angleWindowPerMetreDistanceRad = 50f * Mathf.Deg2Rad;

            direction.Normalize(); // so that the dot is accurate

            // candidate characters are rewound to the earlier state
            foreach (Character character in Netplay.singleton.players)
            {
                if (character && character != owner)
                {
                    int pastStateIndex = character.entity.stateTrack.ClosestIndexBeforeOrEarliest(serverTime - serverPredictionAmount);

                    if (pastStateIndex != -1)
                    {
                        if (Mathf.Acos(Vector3.Dot(direction, (character.entity.stateTrack[pastStateIndex].position - spawnPosition).normalized)) < angleWindowPerMetreDistanceRad / Vector3.Distance(spawnPosition, character.transform.position))
                        {
                            originalCharacterStates.Add(new PastCharacter() { character = character, originalState = character.MakeState() });
                            character.ApplyState(character.entity.stateTrack[pastStateIndex]);
                        }
                    }
                }
            }
        }

        // Run the hitscan against damageables
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

        InitVfx();
        endPointWasSet = true;
    }

    private void InitVfx()
    {
        Vector3 position = transform.position;
        tubeEffect.position = (position + endPoint.transform.position) * 0.5f;
        tubeEffect.localScale = new Vector3(1f, 1f, Vector3.Distance(position, endPoint.transform.position) * tubeEffectScaleFactor);
        tubeEffect.rotation = Quaternion.LookRotation(transform.forward);
    }
}
