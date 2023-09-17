using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera singleton { get; private set; }

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

    [Header("Loopy")]
    /// <summary>
    /// Dampening factor for the up vector while running around walls/ceilings
    /// </summary>
    public float loopyDampFactor = 0.1f;

    public Camera unityCamera { get; private set; }

    /// <summary>
    /// Current aim direction of the camera
    /// </summary>
    public Vector3 aimDirection { get; set; } = Vector3.forward;

    public bool isFirstPerson => thirdPersonDistance <= 0f;

    // Character movement "up" on last frame. Used to rotate camera
    private Vector3 lastAimUpdateCharacterUp = Vector3.up;

    private Vector3 interpolatedCharacterUp = Vector3.up;
    private Vector3 interpolatedCharacterUpVelocity = Vector3.zero;

    private float lastPlayerFallSpeed = 0;
    private float lastPlayerTimeInAir = 0;

    private float landBobTimer = 0;
    private float landBobMagnitude = 0;
    private float landBobDuration = 0;

    private void Awake()
    {
        singleton = this;
        unityCamera = GetComponent<Camera>();
    }

    public void UpdateAim()
    {
        // Rotate the camera based on loopy movement
        Vector3 characterUp = Vector3.up;

        if (currentPlayer)
            characterUp = currentPlayer.movement.up;

        interpolatedCharacterUp = Vector3.SmoothDamp(interpolatedCharacterUp, characterUp, ref interpolatedCharacterUpVelocity, loopyDampFactor);

        if (characterUp != lastAimUpdateCharacterUp)
        {
            aimDirection = Quaternion.FromToRotation(lastAimUpdateCharacterUp, interpolatedCharacterUp) * aimDirection;
            lastAimUpdateCharacterUp = interpolatedCharacterUp;
        }

        // Mouselook
        if (GameManager.singleton.canPlayMouselook)
        {
            Vector3 newAim = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * GamePreferences.mouseSpeed, interpolatedCharacterUp) * aimDirection;

            // we need to clamp this...
            const float limit = 1f;
            float degreesFromUp = Mathf.Acos(Vector3.Dot(newAim, interpolatedCharacterUp)) * Mathf.Rad2Deg;
            float verticalAngleDelta = -Input.GetAxis("Mouse Y") * GamePreferences.mouseSpeed;

            if (degreesFromUp + verticalAngleDelta <= limit)
                verticalAngleDelta = limit - degreesFromUp;
            if (degreesFromUp + verticalAngleDelta >= 180f - limit)
                verticalAngleDelta = 180f - limit - degreesFromUp;
            newAim = Quaternion.AngleAxis(verticalAngleDelta, Vector3.Cross(interpolatedCharacterUp, newAim)) * newAim;

            if (currentPlayer && currentPlayer.movement.state == CharacterMovementState.Climbing)
            {
                // it's 10pm, this is what I came up with for the angle limit, let's see if I regret this later
                float angleLimit = 80f;
                float rightLimit = Mathf.Sin(angleLimit * Mathf.Deg2Rad);
                Vector3 aimAlongUpPlane = newAim.AlongPlane(interpolatedCharacterUp).normalized;
                Vector3 aimAlongUp = interpolatedCharacterUp * newAim.AlongAxis(interpolatedCharacterUp);
                Vector3 rightAlongUpPlane = currentPlayer.movement.right.AlongPlane(interpolatedCharacterUp).normalized;
                Vector3 forwardAlongUpPlane = currentPlayer.movement.forward.AlongPlane(interpolatedCharacterUp).normalized;

                if (Vector3.Dot(aimAlongUpPlane, rightAlongUpPlane) > rightLimit)
                {
                    aimAlongUpPlane = rightAlongUpPlane * rightLimit + forwardAlongUpPlane * (Mathf.Sqrt(1f - (rightLimit * rightLimit)));
                    newAim = aimAlongUpPlane * (Mathf.Sqrt(1f - aimAlongUp.magnitude * aimAlongUp.magnitude)) + aimAlongUp;
                }
                else if (Vector3.Dot(aimAlongUpPlane, rightAlongUpPlane) < -rightLimit)
                {
                    aimAlongUpPlane = rightAlongUpPlane * -rightLimit + forwardAlongUpPlane * (Mathf.Sqrt(1f - (rightLimit * rightLimit)));
                    newAim = aimAlongUpPlane * (Mathf.Sqrt(1f - aimAlongUp.magnitude * aimAlongUp.magnitude)) + aimAlongUp;
                }
            }

            // center cam button
            if (GameManager.singleton.input.Gameplay.CenterCamera.ReadValue<float>() > 0.5f)
                newAim.SetAlongAxis(interpolatedCharacterUp, 0);

            // final new value
            aimDirection = newAim.normalized;

            if (aimDirection.sqrMagnitude == 0f)
                aimDirection = Vector3.forward; // this can happen
        }
    }

    void LateUpdate()
    {
        if (currentPlayer == null)
        {
            if (Netplay.singleton.localPlayerId >= 0)
                currentPlayer = Netplay.singleton.players[Netplay.singleton.localPlayerId]; // follow the player in this world
        }

        if (currentPlayer)
        {
            Vector3 characterUp = currentPlayer.GetComponent<PlayerCharacterMovement>().up;

            // Move and rotate to player position
            transform.position = currentPlayer.transform.position + characterUp * eyeHeight;
            transform.rotation = Quaternion.LookRotation(aimDirection, interpolatedCharacterUp);

            if (GameManager.singleton.isPaused)
                transform.rotation = Quaternion.Euler(0, 180, 0) * transform.rotation; // look towards the character when paused/potentially character config

            // Apply zoom in/out
            if (GameManager.singleton.input.Gameplay.ZoomOut.ReadValue<float>() > 0f)
            {
                if (thirdPersonDistance == 0f)
                    thirdPersonDistance = minThirdPersonDistance;
                else
                    thirdPersonDistance = Mathf.Min(thirdPersonDistance + zoomSpeed, maxThirdPersonDistance);
            }
            else if (GameManager.singleton.input.Gameplay.ZoomIn.ReadValue<float>() > 0f)
            {
                if (thirdPersonDistance - zoomSpeed < minThirdPersonDistance)
                    thirdPersonDistance = 0f;
                else
                    thirdPersonDistance = Mathf.Max(thirdPersonDistance - zoomSpeed, minThirdPersonDistance);
            }

            float currentThirdPersonDistance = thirdPersonDistance;
            if (GameManager.singleton.isPaused)
                currentThirdPersonDistance = characterPreviewDistance; // when paused and possibly customising character, zoom out

            // Apply third-person view
            if (currentThirdPersonDistance > 0f)
            {
                Vector3 targetPosition = transform.position - transform.forward * currentThirdPersonDistance;

                if (Physics.SphereCast(transform.position, collisionRadius, targetPosition - transform.position, out RaycastHit hit, currentThirdPersonDistance, collisionLayers, QueryTriggerInteraction.Ignore))
                    targetPosition = hit.point + hit.normal * collisionRadius;

                transform.position = targetPosition;

                currentPlayer.isInvisible = false; // show our own model in third-person
                currentPlayer.damageable.doInvincibilityBlink = true;
            }
            else
            {
                currentPlayer.isInvisible = true; // hide our own model in first-person
                currentPlayer.damageable.doInvincibilityBlink = false; // don't let invincibility blink unhide it
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

                transform.position += transform.up * (Mathf.Sin(eyeBobSpeed * (Time.time - lastPlayerTimeInAir) * Mathf.Deg2Rad) * eyeBobHeight * Mathf.Min(1, currentPlayer.movement.groundVelocity.magnitude / maxPlayerVelocityForEyeBob));
            }

            lastPlayerFallSpeed = currentPlayer.movement.isOnGround ? 0 : Mathf.Max(-currentPlayer.movement.velocity.y, 0);
            lastPlayerTimeInAir = currentPlayer.movement.isOnGround ? lastPlayerTimeInAir : Time.time; // means that the camera bob phase will begin the moment you land, making smoother landings
        }
        else
        {
            // spectator cam goes here?
        }
    }
}
