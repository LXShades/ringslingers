using System.Collections.Generic;
using UnityEngine;

namespace Ringslingers.Tests
{
    [SelectionBase]
    public class PlayerCharMovementTest : PlayerCharacterMovement
    {
        private FreeCam cam;
        private MeshFilter meshFilter;

        [Header("Simulation")]
        public bool enableManualControl = false;
        public float manualCamDistance = 2f;
        public int numSteps = 1;
        [Range(0.01f, 0.2f)]
        public float stepSize = 0.1f;
        public Material debugMaterial;
        [Range(0f, 1f), Tooltip("As percentage of max speed")]
        public float startVelocity;
        public List<SimpleInput> controlsOverTime = new List<SimpleInput>(new[] { new SimpleInput() { time = 0f, isJumpDown = false, verticalMovement = 1f } });

        [System.Serializable]
        public struct SimpleInput
        {
            public float time;
            public bool isJumpDown;
            public float verticalMovement;

            public CharacterInput ToPlayerInput(Vector3 cameraForward)
            {
                return new CharacterInput()
                {
                    aimDirection = cameraForward,
                    btnJump = isJumpDown,
                    moveVerticalAxis = verticalMovement,
                };
            }
        }

        CharacterInput lastInput;

        protected override void Awake()
        {
            base.Awake();

            cam = FindFirstObjectByType<FreeCam>();
            meshFilter = GetComponentInChildren<MeshFilter>();
        }

        private void Update()
        {
            CapsuleCollider capsule = (colliders[0] as CapsuleCollider);
            DebugDraw.DrawCapsule(transform.position + Vector3.up * (capsule.center.y - capsule.height * 0.5f), transform.position + Vector3.up * (capsule.center.y + capsule.height * 0.5f), capsule.radius, Color.red);

            forward = cam.forward;
            if (enableManualControl)
            {
                CharacterInput nextInput = CharacterInput.MakeLocalInput(lastInput);
                nextInput.aimDirection = cam.forward;

                TickMovement(Time.deltaTime, nextInput.WithDeltas(lastInput), new TickInfo() { isForwardTick = false, isFullTick = true });

                lastInput = nextInput;
                lastInput.aimDirection = forward;

                cam.transform.position = transform.position + up * 0.5f - forward * manualCamDistance;
            }
            else
            {
                Vector3 startPosition = transform.position;
                Quaternion startRotation = transform.rotation;
                CharacterInput lastInput = controlsOverTime[0].ToPlayerInput(transform.forward);

                for (int i = 0; i < numSteps; i++)
                {
                    CharacterInput nextInput = default;

                    for (int j = 0; j < controlsOverTime.Count; j++)
                    {
                        if (controlsOverTime[j].time <= i * stepSize)
                            nextInput = controlsOverTime[j].ToPlayerInput(transform.forward);
                    }

                    TickMovement(stepSize, nextInput.WithDeltas(lastInput), new TickInfo() { isForwardTick = false, isFullTick = true });
                    Graphics.DrawMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, debugMaterial, gameObject.layer, null, 0, null, false, false, false);
                }

                transform.position = startPosition;
                transform.rotation = startRotation;
                velocity = transform.forward * (startVelocity * topSpeed);
            }
        }

        private void LateUpdate()
        {
        }
    }
}
