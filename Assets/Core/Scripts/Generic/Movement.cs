using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [Header("Collision")]
    public bool enableCollision = true;

    [Header("Note: Colliders cannot currently rotate")]
    [Tooltip("List of colliders to be used in collision tests")]
    public Collider[] colliders = new Collider[0];

    [Tooltip("List of collision layers to interact with")]
    public LayerMask blockingCollisionLayers = ~0;

    [Header("Physics")]
    [Tooltip("If selected, this component will not move the object but will supply the Move function")]
    public bool useManualPhysics = false;

    [Range(0, 1)]
    public float bounceFactor = 0f;

    [Range(0, 1)]
    public float bounceFriction = 0;

    public float gravityMultiplier = 1f;

    /// <summary>
    /// The current velocity of the object
    /// </summary>
    [HideInInspector] public Vector3 velocity;

    void Update()
    {
        if (!useManualPhysics)
        {
            // Do le gravity
            velocity.y -= GameManager.singleton.gravity * Time.deltaTime * gravityMultiplier;

            // Do le move
            RaycastHit hit;

            if (Move(velocity * Time.deltaTime, out hit))
            {
                Vector3 resistanceVector = hit.normal * (-Vector3.Dot(hit.normal, velocity) * (1f + bounceFactor));
                Vector3 frictionVector = -velocity * bounceFriction;

                frictionVector -= hit.normal * Vector3.Dot(hit.normal, frictionVector);

                velocity += resistanceVector + frictionVector;
            }
        }
    }

    HashSet<IMovementCollisions> movementCollisions = new HashSet<IMovementCollisions>();
    RaycastHit[] hits = new RaycastHit[16];

    /// <summary>
    /// Moves with collision checking. Can be a computationally expensive operation
    /// </summary>
    /// <param name="offset"></param>
    public bool Move(Vector3 offset, out RaycastHit hitOut, bool isReconciliation = false)
    {
        hitOut = new RaycastHit();

        if (offset == Vector3.zero)
            return false;
        if (!enableCollision)
        {
            transform.position += offset;
            return true; // that was easy
        }

        const bool kDrawDebug = false;
        const float kSkin = 0.005f;
        const int kNumIterations = 3;
        Vector3 currentMovement = offset;
        bool hasHitOccurred = false;
        Color[] colorByStage = new Color[] { Color.red, Color.green, Color.blue, Color.yellow };

        movementCollisions.Clear();

        for (int iteration = 0; iteration < kNumIterations; iteration++)
        {
            RaycastHit hit;
            float currentMovementMagnitude = currentMovement.magnitude;
            float kPullback = iteration == 0 ? 1f : 0f;
            Vector3 normalMovement = currentMovement.normalized;

            int numHits = ColliderCast(hits, transform.position, normalMovement, currentMovementMagnitude + kSkin, blockingCollisionLayers, QueryTriggerInteraction.Collide, kPullback, kDrawDebug, colorByStage[iteration]);
            float lowestDist = currentMovementMagnitude + kSkin;
            int lowestHitId = -1;

            for (int i = 0; i < numHits; i++)
            {
                if (true) //hits[i].distance >= -0.001f) - basically, when collider rotation is possible, things can break... colliders can rotate through the ground
                {
                    // find closest blocking collider
                    if (!hits[i].collider.isTrigger && hits[i].distance < lowestDist)
                    {
                        lowestDist = hits[i].distance;
                        lowestHitId = i;
                    }

                    // acknowledge all collided movementcollision objects
                    foreach (IMovementCollisions movementCollision in hits[i].collider.GetComponents<IMovementCollisions>())
                        movementCollisions.Add(movementCollision);
                }
            }

            // Identify the closest blocking collisiond
            if (lowestHitId != -1)
            {
                hit = hits[lowestHitId];
                hitOut = hit;
                hasHitOccurred = true;

                if (iteration == kNumIterations - 1)
                {
                    // final collision: block further movement entirely
                    currentMovement = currentMovement.normalized * (hit.distance - kSkin);
                }
                else
                {
                    // use slidey slidey collision
                    currentMovement += hit.normal * (-Vector3.Dot(hit.normal, currentMovement.normalized * (currentMovement.magnitude - hit.distance)) + kSkin);
                }

                if (kDrawDebug)
                {
                    Color finalColour = Color.Lerp(colorByStage[iteration], Color.white, 0.5f);
                    DebugExtension.DebugPoint(transform.position + currentMovement, finalColour, 0.15f + iteration * 0.05f);
                    DebugExtension.DebugWireSphere(transform.position + currentMovement, finalColour, 0.15f + iteration * 0.05f);
                }
            }
            else
            {
                if (kDrawDebug)
                {
                    Color finalColour = Color.Lerp(colorByStage[iteration], Color.white, 0.5f);
                    DebugExtension.DebugPoint(transform.position + currentMovement, finalColour, 0.15f + iteration * 0.05f);
                    DebugExtension.DebugWireSphere(transform.position + currentMovement, finalColour, 0.15f + iteration * 0.05f);
                }

                break;
            }
        }

        // Do the move
        transform.position += currentMovement;

        foreach (IMovementCollisions collisions in movementCollisions)
        {
            collisions.OnMovementCollidedBy(this, isReconciliation);
        }

        return hasHitOccurred;
    }

    RaycastHit[] hitBuffer = new RaycastHit[128];

    public int ColliderCast(RaycastHit[] hitsOut, Vector3 startPosition, Vector3 castDirection, float castMaxDistance, int layers, QueryTriggerInteraction queryTriggerInteraction, float pullback = 0f, bool drawDebug = false, Color drawDebugColor = default)
    {
        Matrix4x4 toWorld = transform.localToWorldMatrix;
        int numHits = 0;

        castDirection.Normalize();
        startPosition -= castDirection * pullback;
        castMaxDistance += pullback;

        toWorld = Matrix4x4.Translate(startPosition - transform.position) * toWorld;

        foreach (Collider collider in colliders)
        {
            int colliderNumHits = 0;
            if (collider is SphereCollider sphere)
            {
                float radius = sphere.radius * Mathf.Max(Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), transform.lossyScale.z);
                colliderNumHits = Physics.SphereCastNonAlloc(
                    toWorld.MultiplyPoint(sphere.center),
                    radius,
                    castDirection,
                    hitBuffer,
                    castMaxDistance,
                    layers,
                    queryTriggerInteraction);

                if (drawDebug)
                {
                    DebugExtension.DebugCapsule(toWorld.MultiplyPoint(sphere.center) - castDirection * radius, toWorld.MultiplyPoint(sphere.center) + castDirection * (castMaxDistance + radius), drawDebugColor, radius);
                }
            }
            else if (collider is CapsuleCollider capsule)
            {
                Vector3 up = (capsule.direction == 0 ? transform.right : (capsule.direction == 1 ? transform.up : transform.forward)) * (Mathf.Max(capsule.height * 0.5f - capsule.radius, 0));
                Vector3 center = toWorld.MultiplyPoint(capsule.center);

                colliderNumHits = Physics.CapsuleCastNonAlloc(
                    center + up, center - up,
                    capsule.radius * 0.5f * Mathf.Max(Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), transform.lossyScale.z),
                    castDirection,
                    hitBuffer,
                    castMaxDistance,
                    layers,
                    queryTriggerInteraction);
            }
            else
            {
                continue; // couldn't detect collider type
            }

            for (int i = 0; i < colliderNumHits && numHits < hitsOut.Length; i++)
            {
                if (hitBuffer[i].collider.gameObject == collider.gameObject)
                    continue; // avoid colliding with self...
                if (Vector3.Dot(hitBuffer[i].normal, castDirection) > 0.01f)
                    continue; // in pullback collisions, don't collide with things we wouldn't actually crash into
                if (hitBuffer[i].distance <= 0f)
                    continue; // this type of collision normally seems to happen when we're already inside them, which doesn't work nicely with pullback collisions

                hitsOut[numHits] = hitBuffer[i];
                hitsOut[numHits].distance -= pullback;
                numHits++;
            }
        }

        return numHits;
    }
}

public interface IMovementCollisions
{
    void OnMovementCollidedBy(Movement source, bool isReconciliation);
}