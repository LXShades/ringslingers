using UnityEngine;

namespace Ringslingers.Tests
{
    public class StairMovement : MonoBehaviour
    {
        private MeshFilter meshFilter;
        public Material debugMaterial;
        public UnityEngine.UI.Text debugText;
        private Movement movement;
        private FreeCam cam;

        public Vector2 input = new Vector3(0f, 1f);

        private Vector3 up;
        private Vector3 forward;
        private Vector3 right => Vector3.Cross(up, forward).normalized;

        [Header("Velocities")]
        public float acceleration = 10f;
        public float friction = 10f;
        public float gravity = 10f;

        [Header("Grounding")]
        public float groundSphereTestRadius = 0.25f;
        public float groundTestDistanceThreshold = 0.05f;
        public float groundEscapeThreshold = 3f;
        public float slipRadius = 0.25f;
        public float slipVelocity = 5f;

        [Header("Loopy")]
        public float loopyGroundTestDistance = 0.5f;

        [Header("Enable")]
        public bool enableSlip = true;
        public bool enableFriction = true;
        public bool enableCollisionsAffectVelocity = true;
        public bool enableLoopy = true;

        [Header("Simulation")]
        public bool enableManualControl = false;
        public int numSteps = 1;
        public float stepSize = 0.1f;

        //RaycastHit[] hitBuffer = new RaycastHit[32];

        void Awake()
        {
            movement = GetComponent<Movement>();
            meshFilter = GetComponentInChildren<MeshFilter>();
            cam = FindObjectOfType<FreeCam>();
        }

        private void Update()
        {
            Vector3 initialPosition = transform.position;
            Quaternion initialRotation = transform.rotation;
            Physics.SyncTransforms();

            MovementDebugStats.Snapshot stats = MovementDebugStats.total;
            if (enableManualControl)
            {
                Vector3 oldUp = up;
                input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                forward = Camera.main.transform.forward;
                Tick(Time.deltaTime);
                cam.transform.position = transform.position + up - cam.transform.forward * 4f;
                cam.forward = Quaternion.FromToRotation(oldUp, up) * cam.forward;
                cam.up = up;
            }
            else
            {
                for (int i = 0; i < numSteps; i++)
                {
                    Vector3 oldUp = up;
                    Tick(stepSize);
                    forward = Quaternion.FromToRotation(oldUp, up) * forward;

                    Graphics.DrawMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, debugMaterial, gameObject.layer, null, 0, null, false, false, false);
                }

                transform.position = initialPosition;
                transform.rotation = initialRotation;
                up = Vector3.up;
                forward = initialRotation * Vector3.forward;
                movement.velocity = Vector3.zero;
            }

            if (debugText)
                debugText.text = MovementDebugStats.total.Since(stats).ToString();

            Physics.SyncTransforms();
        }

        public void Tick(float deltaTime)
        {
            Physics.SyncTransforms();

            // Do floor test
            bool hasGroundCastHit = Physics.SphereCast(new Ray(transform.position + up * groundSphereTestRadius, -up), groundSphereTestRadius, out RaycastHit groundHit, Mathf.Max(groundSphereTestRadius, loopyGroundTestDistance), ~0, QueryTriggerInteraction.Ignore);
            bool isOnGround = false;
            bool isSlipping = false;
            bool isLoopy = false;
            Vector3 groundNormal = Vector3.up;
            Vector3 loopyNormal = Vector3.up;
            Vector3 slipVector = Vector3.zero;

            if (hasGroundCastHit)
            {
                // anywhere on the circle could be hit, we'll just test if the contact point is "close enough" to the vertical distance test threshold
                float primaryDistance = -Vector3.Dot(groundHit.point - transform.position, up);
                float secondaryDistance = (transform.position - groundHit.point).AlongPlane(up).magnitude;

                if (Vector3.Dot(groundHit.normal, movement.velocity) < groundEscapeThreshold)
                {
                    Debug.DrawLine(transform.position, groundHit.point, Color.red);
                    if (primaryDistance < groundTestDistanceThreshold)
                    {
                        isOnGround = true;
                        groundNormal = groundHit.normal;
                    }
                    else if (secondaryDistance > slipRadius)
                    {
                        isSlipping = true;
                        slipVector = (transform.position - groundHit.point).AlongPlane(up).normalized;
                    }
                }

                if (primaryDistance <= loopyGroundTestDistance)
                {
                    if (Physics.Raycast(new Ray(transform.position + up * 0.01f, -up), out RaycastHit rayHit, loopyGroundTestDistance, ~0, QueryTriggerInteraction.Ignore) && rayHit.distance <= loopyGroundTestDistance)
                    {
                        isLoopy = true;
                        loopyNormal = rayHit.normal;

                        Debug.DrawLine(transform.position + new Vector3(0, 2f, 0f), transform.position + new Vector3(0, 2f, 0) + loopyNormal, Color.red);
                    }
                }
            }

            Debug.DrawLine(transform.position + groundNormal * 2f, transform.position - groundNormal * 2f, isOnGround ? Color.green : Color.blue);
            DrawCircle(transform.position, groundSphereTestRadius, Color.white);

            // Accelerate
            Vector3 inputDir = Vector3.ClampMagnitude(forward.AlongPlane(groundNormal).normalized * input.y + right.AlongPlane(groundNormal).normalized * input.x, 1f);
            movement.velocity += inputDir * ((acceleration + friction) * deltaTime);

            // Friction
            Vector3 groundVelocity = movement.velocity.AlongPlane(groundNormal);
            float groundVelocityMagnitude = groundVelocity.magnitude;

            if (groundVelocityMagnitude > 0f && enableFriction)
                movement.velocity -= groundVelocity * Mathf.Min(friction * deltaTime / groundVelocityMagnitude, 1f);

            // Grounding and gravity
            if (isOnGround)
                movement.velocity = movement.velocity.AlongPlane(groundNormal); // along gravity vector, may be different for wall running
            else
            {
                if (isSlipping && enableSlip)
                {
                    // Apply slip vector if there is one
                    movement.velocity.SetAlongAxis(slipVector, slipVelocity);
                }

                // Gravity - only apply when not on ground, otherwise slipping occurs
                movement.velocity += new Vector3(0f, -gravity * deltaTime, 0f);
            }

            // Do final movement
            if (movement.Move(movement.velocity * deltaTime, out Movement.Hit hitOut))
            {
                if (enableCollisionsAffectVelocity && deltaTime > 0f)
                {
                    //movement.velocity = (transform.position - positionBeforeMovement) / deltaTime;
                    movement.velocity.SetAlongAxis(hitOut.normal, 0f);
                }
            }

            if (isLoopy)
                up = loopyNormal;
            else
                up = Vector3.up;

            transform.rotation = Quaternion.LookRotation(forward.AlongPlane(up), up);
        }

        private void DrawCircle(Vector3 position, float radius, Color color)
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12;
                float nextAngle = (i + 1) * Mathf.PI * 2f / 12;
                Debug.DrawLine(position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius, position + new Vector3(Mathf.Sin(nextAngle), 0f, Mathf.Cos(nextAngle)) * radius, color);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
        }
    }
}
