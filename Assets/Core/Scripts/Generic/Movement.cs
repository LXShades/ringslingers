using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;

public class Movement : WorldObjectComponent
{
    [Header("Collision")]
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

    public override void FrameUpdate()
    {
        if (!useManualPhysics)
        {
            // Do le gravity
            velocity.y -= GameManager.singleton.gravity * World.live.deltaTime * gravityMultiplier;

            // Do le move
            RaycastHit hit;

            if (Move(velocity * World.live.deltaTime, out hit))
            {
                Vector3 resistanceVector = hit.normal * (-Vector3.Dot(hit.normal, velocity) * (1f + bounceFactor));
                Vector3 frictionVector = -velocity * bounceFriction;

                frictionVector -= hit.normal * Vector3.Dot(hit.normal, frictionVector);

                velocity += resistanceVector + frictionVector;
            }
        }
    }

    /// <summary>
    /// Moves with collision checking. Can be a computationally expensive operation - be advised
    /// </summary>
    /// <param name="offset"></param>
    public bool Move(Vector3 offset, out RaycastHit hitOut)
    {
        hitOut = new RaycastHit();

        if (offset == Vector3.zero)
            return false;

        int numIterations = 2;
        Vector3 currentMovement = offset;
        bool hasHitOccurred = false;

        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            RaycastHit hit = new RaycastHit();
            RaycastHit[] hits;
            float movementMagnitude = currentMovement.magnitude;
            float lowestDist = float.MaxValue;
            int lowestHitId = -1;

            foreach (Collider collider in colliders)
            {
                if (collider.GetType() == typeof(SphereCollider))
                {
                    SphereCollider sphere = collider as SphereCollider;
                    hits = Physics.SphereCastAll(
                        transform.TransformPoint(sphere.center),
                        sphere.radius * Mathf.Max(Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), transform.lossyScale.z),
                        currentMovement,
                        movementMagnitude + 0.0001f,
                        blockingCollisionLayers,
                        QueryTriggerInteraction.Ignore);
                }
                else if (collider.GetType() == typeof(CapsuleCollider))
                {
                    CapsuleCollider capsule = collider as CapsuleCollider;
                    Vector3 up = (capsule.direction == 0 ? transform.right : (capsule.direction == 1 ? transform.up : transform.forward)) * (Mathf.Max(capsule.height * 0.5f - capsule.radius, 0));
                    Vector3 center = transform.TransformPoint(capsule.center);

                    hits = Physics.CapsuleCastAll(
                        center + up, center - up,
                        capsule.radius * 0.5f * Mathf.Max(Mathf.Max(transform.lossyScale.x, transform.lossyScale.y), transform.lossyScale.z),
                        currentMovement,
                        movementMagnitude + 0.0001f,
                        blockingCollisionLayers,
                        QueryTriggerInteraction.Ignore);
                }
                else
                {
                    continue; // couldn't detect collider type
                }

                // Get the current closest one
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].distance > 0 && hits[i].distance < lowestDist) // kinda hacky: 0 seems to mean we're stuck inside something and das is nicht gut
                    {
                        lowestDist = hits[i].distance;
                        lowestHitId = i;
                    }
                }

                if (lowestHitId != -1)
                {
                    hit = hits[lowestHitId];
                    hitOut = hit;
                    hasHitOccurred = true;
                }
            }

            if (lowestDist != float.MaxValue)
            {
                if (iteration == numIterations - 1)
                {
                    currentMovement = currentMovement.normalized * (hit.distance - 0.001f);
                }
                else
                {
                    // use slidey slidey collision
                    currentMovement += hit.normal
                        * (-Vector3.Dot(hit.normal, currentMovement.normalized * (currentMovement.magnitude - hit.distance)) + 0.01f);
                }
            }
            else
            {
                break;
            }
        }

        // Do the move
        transform.position += currentMovement;

        return hasHitOccurred;
    }
}
