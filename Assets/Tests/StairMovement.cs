using UnityEngine;

namespace Ringslingers.Tests
{
    public class StairMovement : CharacterMovement
    {
        private MeshFilter meshFilter;
        public Material debugMaterial;
        public UnityEngine.UI.Text debugText;
        private FreeCam cam;

        private Vector3 spawnPosition;

        [Header("Simulation")]
        public bool enableManualControl = false;
        public int numSteps = 1;
        public float stepSize = 0.1f;

        //RaycastHit[] hitBuffer = new RaycastHit[32];

        protected override void Awake()
        {
            base.Awake();
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
                movement.velocity = Vector3.zero;
            }

            if (debugText)
                debugText.text = MovementDebugStats.total.Since(stats).ToString();

            Physics.SyncTransforms();
        }
    }
}
