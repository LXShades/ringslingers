using UnityEngine;

namespace Ringslingers.Tests
{
    public class CharMovementTest : CharacterMovement
    {
        private MeshFilter meshFilter;
        public Material debugMaterial;
        public UnityEngine.UI.Text debugText;
        private FreeCam cam;

        private Vector3 spawnPosition;

        public Vector2 input = new Vector3(0f, 1f);
        public bool inputJump = false;

        [Header("Velocities")]
        public float acceleration = 10f;
        public float friction = 10f;

        [Header("Simulation")]
        public bool enableManualControl = false;
        public int numSteps = 1;
        public float stepSize = 0.1f;

        [Header("Enable (CharMoveTest)")]
        public bool enableFriction = true;
        public bool enableDebugDrawing = true;

        //RaycastHit[] hitBuffer = new RaycastHit[32];

        void Awake()
        {
            meshFilter = GetComponentInChildren<MeshFilter>();
            cam = FindObjectOfType<FreeCam>();
            spawnPosition = transform.position;
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
                inputJump = Input.GetKeyDown(KeyCode.Space);
                forward = Camera.main.transform.forward;
                Tick(Time.deltaTime);
                cam.transform.position = transform.position + up - cam.transform.forward * 4f;
                cam.forward = Quaternion.FromToRotation(oldUp, up) * cam.forward;
                cam.up = up;

                if (Input.GetKeyDown(KeyCode.R))
                    transform.position = spawnPosition;
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
                velocity = Vector3.zero;
            }

            if (debugText)
                debugText.text = MovementDebugStats.total.Since(stats).ToString();

            Physics.SyncTransforms();
        }

        private void Tick(float deltaTime)
        {
            Physics.SyncTransforms();

            // Do floor test
            GroundInfo groundInfo;

            CalculateGroundInfo(out groundInfo);

            if (enableDebugDrawing)
            {
                Debug.DrawLine(transform.position + up * 2f, transform.position - up * 2f, groundInfo.isOnGround ? Color.green : Color.blue);
                DrawCircle(transform.position, groundSphereTestRadius, Color.white);
                Debug.DrawLine(transform.position, groundInfo.hitPoint, Color.red);

                Debug.DrawLine(transform.position + new Vector3(0, 2f, 0f), transform.position + new Vector3(0, 2f, 0) + groundInfo.loopyNormal, Color.red);
            }

            // Accelerate
            Vector3 inputDir = Vector3.ClampMagnitude(forward.AlongPlane(groundInfo.normal).normalized * input.y + right.AlongPlane(groundInfo.normal).normalized * input.x, 1f);
            velocity += inputDir * ((acceleration + friction) * deltaTime);

            // Friction
            Vector3 groundVelocity = velocity.AlongPlane(groundInfo.normal);
            float groundVelocityMagnitude = groundVelocity.magnitude;

            if (groundVelocityMagnitude > 0f && enableFriction)
                velocity -= groundVelocity * Mathf.Min(friction * deltaTime / groundVelocityMagnitude, 1f);

            // Jump
            if (groundInfo.isOnGround && inputJump)
                velocity.SetAlongAxis(up, 9.8f);

            Vector3 preMovePosition = transform.position;

            ApplyCharacterVelocity(in groundInfo, deltaTime);

            if (enableDebugDrawing && groundInfo.isLoopy && enableLoopyPushdown)
            {
                float raycastLength = Vector3.Distance(transform.position, preMovePosition);
                Debug.DrawLine(preMovePosition, transform.position, Color.white);
                Debug.DrawLine(transform.position + transform.forward * 0.02f, transform.position + transform.forward * 0.02f - up * raycastLength, Color.white);
                Debug.DrawLine(transform.position + transform.forward * 0.02f - up * raycastLength, preMovePosition, Color.white);
            }

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
    }
}
