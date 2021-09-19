using UnityEngine;

namespace Ringslingers.Tests
{
    [SelectionBase]
    public class PlayerCharMovementTest : PlayerCharacterMovement
    {
        private FreeCam cam;

        [Header("Simulation")]
        public bool enableManualControl = false;
        public float manualCamDistance = 2f;
        public int numSteps = 1;
        public float stepSize = 0.1f;

        PlayerInput lastInput;

        private void Awake()
        {
            cam = FindObjectOfType<FreeCam>();
        }

        private void Update()
        {
            if (enableManualControl)
            {
                PlayerInput nextInput = PlayerInput.MakeLocalInput(lastInput, up);

                TickMovement(Time.deltaTime, nextInput.WithDeltas(lastInput), false);

                lastInput = nextInput;
                lastInput.aimDirection = forward;
            }

            cam.transform.position = transform.position + up * 0.5f - forward * manualCamDistance;
            cam.forward = forward;
            cam.up = up;
        }

        private void LateUpdate()
        {
        }
    }
}
