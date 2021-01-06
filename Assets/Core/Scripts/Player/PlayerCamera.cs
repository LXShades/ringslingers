using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    /// <summary>
    /// Player we're currently following
    /// </summary>
    public Player currentPlayer;

    [Header("Zoom")]
    public float zoomSpeed = 1f;
    public float thirdPersonDistance = 0f;

    public float maxThirdPersonDistance = 5f;
    public float minThirdPersonDistance = 1f;

    [Header("Position")]
    /// <summary>
    /// The height of the camera relative to the player's feet
    /// </summary>
    public float eyeHeight = 0.6f;

    [Header("Head bob")]
    /// <summary>
    /// The height (positive and negative) of eye bobs
    /// </summary>
    public float eyeBobHeight = 0.1f;

    /// <summary>
    /// The speed of eye bobs, in degrees per second
    /// </summary>
    public float eyeBobSpeed = 630;

    /// <summary>
    /// Maximum player velocity for maximum eye bob
    /// </summary>
    public float maxPlayerVelocityForEyeBob = 30;

    /// <summary>
    /// The max eye bob height when landing
    /// </summary>
    public float landEyeBobHeight = 0.3f;

    /// <summary>
    /// Maximum player landing speed for maximum eye bob
    /// </summary>
    public float maxPlayerLandForEyeBob = 30;

    private float lastPlayerFallSpeed = 0;

    private float landBobTimer = 0;
    private float landBobMagnitude = 0;
    private float landBobDuration = 0;

    void LateUpdate()
    {
        if (currentPlayer == null)
        {
            if (Netplay.singleton.localPlayerId >= 0)
                currentPlayer = Netplay.singleton.players[Netplay.singleton.localPlayerId]; // follow the player in this world
        }

        if (currentPlayer)
        {
            // Move and rotate to player position
            transform.position = currentPlayer.transform.position + Vector3.up * eyeHeight;
            transform.rotation = Quaternion.Euler(currentPlayer.input.verticalAim, currentPlayer.input.horizontalAim, 0);

            // actually try 3D rotation
            transform.rotation = Quaternion.LookRotation(currentPlayer.input.aimDirection, currentPlayer.GetComponent<CharacterMovement>().up);

            // Apply zoom in/out
            float zoom = Input.GetAxis("CameraZoom") * zoomSpeed;
            if (zoom != 0f)
            {
                if (zoom > 0)
                {
                    if (thirdPersonDistance == 0f)
                        thirdPersonDistance = minThirdPersonDistance;
                    else
                        thirdPersonDistance = Mathf.Min(thirdPersonDistance + zoom, maxThirdPersonDistance);
                }
                else
                {
                    if (thirdPersonDistance + zoom < minThirdPersonDistance)
                        thirdPersonDistance = 0f;
                    else
                        thirdPersonDistance = Mathf.Max(thirdPersonDistance + zoom, minThirdPersonDistance);
                }
            }

            // Apply third-person view
            if (thirdPersonDistance > 0f)
            {
                transform.position -= transform.forward * thirdPersonDistance;
                if (!currentPlayer.characterModel.enabled)
                    currentPlayer.characterModel.enabled = true;
            }
            else
            {
                if (currentPlayer.characterModel.enabled)
                    currentPlayer.characterModel.enabled = false;
            }

            // Apply eye bob
            if (currentPlayer.movement.isOnGround && thirdPersonDistance <= Mathf.Epsilon)
            {
                if (lastPlayerFallSpeed > 0)
                {
                    landBobDuration = 0.4f;
                    landBobTimer = landBobDuration;
                    landBobMagnitude = landEyeBobHeight * Mathf.Min(lastPlayerFallSpeed / maxPlayerLandForEyeBob, 1);
                }

                if (landBobTimer > 0)
                {
                    float landProgress = 1 - (landBobTimer / landBobDuration);
                    transform.position += transform.up * (-landBobMagnitude * landProgress * 2 + landBobMagnitude * landProgress * landProgress * 2);
                    landBobTimer = Mathf.Max(landBobTimer - Time.deltaTime, 0);
                }

                transform.position += transform.up * (Mathf.Sin(eyeBobSpeed * Time.unscaledTime * Mathf.Deg2Rad) * eyeBobHeight * Mathf.Min(1, currentPlayer.movement.velocity.Horizontal().magnitude / maxPlayerVelocityForEyeBob));
            }

            lastPlayerFallSpeed = currentPlayer.movement.isOnGround ? 0 : Mathf.Max(-currentPlayer.movement.velocity.y, 0);
        }
        else
        {
            // spectator cam goes here?
        }
    }
}
