using UnityEngine;

namespace Ringslingers.Tests
{
    public class StairMovement : MonoBehaviour
    {
        private MeshFilter meshFilter;
        public Material debugMaterial;
        public UnityEngine.UI.Text debugText;
        private Movement movement;

        public Vector2 input = new Vector3(0f, 1f);

        public float acceleration = 10f;
        public float friction = 10f;
        public float gravity = 10f;
        public float groundSphereTestRadius = 0.25f;
        public float groundTestDistanceThreshold = 0.05f;
        public float groundEscapeThreshold = 3f;
        public float slipRadius = 0.25f;
        public float slipVelocity = 5f;

        [Header("Enable")]
        public bool enableSlip = true;
        public bool enableFriction = true;
        public bool enableCollisionsAffectVelocity = true;

        [Header("Simulation")]
        public bool enableManualControl = false;
        public int numSteps = 1;
        public float stepSize = 0.1f;

        //RaycastHit[] hitBuffer = new RaycastHit[32];

        void Awake()
        {
            movement = GetComponent<Movement>();
            meshFilter = GetComponentInChildren<MeshFilter>();
        }

        private void Update()
        {
            Vector3 initialPosition = transform.position;
            Quaternion initialRotation = transform.rotation;
            Physics.SyncTransforms();

            MovementDebugStats.Snapshot stats = MovementDebugStats.total;
            if (enableManualControl)
            {
                Camera.main.transform.position = transform.position + Vector3.up;
                Vector3 input3D = Camera.main.transform.TransformDirection(new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")));
                input = new Vector2(input3D.x, input3D.z);
                Tick(Time.deltaTime);
            }
            else
            {
                for (int i = 0; i < numSteps; i++)
                {
                    Tick(stepSize);

                    Graphics.DrawMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, debugMaterial, gameObject.layer, null, 0, null, false, false, false);
                }

                transform.position = initialPosition;
                transform.rotation = initialRotation;
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
            Vector3 up = Vector3.up;
            bool hasGroundCastHit = Physics.SphereCast(new Ray(transform.position + up * (groundSphereTestRadius + 0.01f), Vector3.down), groundSphereTestRadius, out RaycastHit groundHit, groundSphereTestRadius + 0.01f, ~0, QueryTriggerInteraction.Ignore);
            bool isOnGround = false;
            bool isSlipping = false;
            Vector3 groundNormal = up;
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
            }

            Debug.DrawLine(transform.position + new Vector3(0f, 2f, 0f), transform.position - new Vector3(0f, 2f, 0f), isOnGround ? Color.green : Color.blue);
            DrawCircle(transform.position, groundSphereTestRadius, Color.white);

            // Accelerate
            Vector3 inputDir = Vector3.ClampMagnitude(transform.forward.AlongPlane(groundNormal).normalized * input.y + transform.right.AlongPlane(groundNormal).normalized * input.x, 1f);
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
            Vector3 positionBeforeMovement = transform.position;
            if (movement.Move(movement.velocity * deltaTime, out Movement.Hit hitOut))
            {
                if (enableCollisionsAffectVelocity && deltaTime > 0f)
                {
                    //movement.velocity = (transform.position - positionBeforeMovement) / deltaTime;
                    movement.velocity.SetAlongAxis(hitOut.normal, 0f);
                }
            }
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
