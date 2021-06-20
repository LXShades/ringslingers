using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    /// <summary>
    /// Player we're currently following
    /// </summary>
    public Character currentPlayer;

    [Header("Zoom")]
    public float zoomSpeed = 1f;
    public float thirdPersonDistance = 0f;

    public float characterPreviewDistance = 2f;
    public float maxThirdPersonDistance = 5f;
    public float minThirdPersonDistance = 1f;

    [Header("Position")]
    /// <summary>
    /// The height of the camera relative to the player's feet
    /// </summary>
    public float eyeHeight = 0.6f;

    /// <summary>
    /// What does the camera collide with to keep the target in view?
    /// </summary>
    public LayerMask collisionLayers;

    public float collisionRadius = 0.1f;

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
            Vector3 characterUp = currentPlayer.GetComponent<CharacterMovement>().up;
            Vector3 effectiveAimDirection = currentPlayer.liveInput.aimDirection;

            if (GameManager.singleton.isPaused)
                effectiveAimDirection = -currentPlayer.liveInput.aimDirection; // look towards the character when paused/potentially character config

            // Move and rotate to player position
            transform.position = currentPlayer.transform.position + characterUp * eyeHeight;
            transform.rotation = Quaternion.LookRotation(effectiveAimDirection, characterUp);

            // Apply zoom in/out
            float zoom = GameManager.singleton.input.Gameplay.Zoom.ReadValue<float>() * zoomSpeed;
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

            float currentThirdPersonDistance = thirdPersonDistance;
            if (GameManager.singleton.isPaused)
                currentThirdPersonDistance = characterPreviewDistance; // when paused and possibly customising character, zoom out

            // Apply third-person view
            if (currentThirdPersonDistance > 0f)
            {
                Vector3 targetPosition = transform.position - transform.forward * currentThirdPersonDistance;

                if (Physics.Raycast(transform.position, targetPosition - transform.position, out RaycastHit hit, currentThirdPersonDistance, collisionLayers, QueryTriggerInteraction.Ignore))
                    targetPosition = hit.point + hit.normal * collisionRadius;

                transform.position = targetPosition;

                currentPlayer.isInvisible = false; // show our own model in third-person
            }
            else
            {
                currentPlayer.isInvisible = true; // hide our own model in first-person
            }

            // Apply eye bob
            if (currentPlayer.movement.isOnGround && currentThirdPersonDistance <= Mathf.Epsilon)
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

                transform.position += transform.up * (Mathf.Sin(eyeBobSpeed * Time.unscaledTime * Mathf.Deg2Rad) * eyeBobHeight * Mathf.Min(1, currentPlayer.movement.groundVelocity.magnitude / maxPlayerVelocityForEyeBob));
            }

            lastPlayerFallSpeed = currentPlayer.movement.isOnGround ? 0 : Mathf.Max(-currentPlayer.movement.velocity.y, 0);
        }
        else
        {
            // spectator cam goes here?
        }
    }
}
