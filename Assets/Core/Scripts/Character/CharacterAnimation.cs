using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    private PlayerCharacterMovement movement;
    private Character player;
    private Animator animator;

    [Header("Body parts")]
    public Transform root;
    public Transform torso;
    public Transform head;

    [Header("Settings")]
    public float legTurnDegreesPerSecond = 360f;
    public float fallTiltDegreesPerSecond = 50f;
    public float fallTiltMaxDegrees = 20f;
    [Tooltip("As a dot product. A blend between neutral and turning tilt angle where the full angle is lerped until this dot product between speed and direction is met.")]
    public float glideTiltSpeedBlend = 0.05f;

    private Quaternion lastRootRotation = Quaternion.identity;
    private Vector3 lastCharacterUp = Vector3.up;

    private int propHorizontalSpeed = Animator.StringToHash("HorizontalSpeed");
    private int propHorizontalForwardSpeed = Animator.StringToHash("HorizontalForwardSpeed");
    private int propIsOnGround = Animator.StringToHash("IsOnGround");
    private int propIsRolling = Animator.StringToHash("IsRolling");
    private int propIsSpringing = Animator.StringToHash("IsSpringing");
    private int propIsFreeFalling = Animator.StringToHash("IsFreeFalling");
    private int propIsHurt = Animator.StringToHash("IsHurt");
    private int propIsGliding = Animator.StringToHash("IsGliding");
    private int propSpinSpeed = Animator.StringToHash("SpinSpeed");

    private void Start()
    {
        movement = GetComponentInParent<PlayerCharacterMovement>();
        player = GetComponentInParent<Character>();
        animator = GetComponentInParent<Animator>();
    }

    private void Update()
    {
        Vector3 groundVelocity = movement.groundVelocity;
        float forwardSpeedMultiplier = Vector3.Dot(transform.forward.Horizontal(), groundVelocity) <= 0f ? -1 : 1;
        float spinSpeed = 15f;

        if ((movement.state & (PlayerCharacterMovement.State.Rolling | PlayerCharacterMovement.State.SpinCharging)) != 0f)
        {
            spinSpeed = Mathf.Max(movement.velocity.magnitude, movement.spindashChargeLevel * movement.spindashMaxSpeed);
        }

        animator.SetFloat(propHorizontalSpeed, groundVelocity.magnitude);
        animator.SetFloat(propHorizontalForwardSpeed, groundVelocity.magnitude * forwardSpeedMultiplier);
        animator.SetBool(propIsOnGround, movement.isOnGround);
        animator.SetBool(propIsRolling, (movement.state & (PlayerCharacterMovement.State.Jumped | PlayerCharacterMovement.State.Rolling | PlayerCharacterMovement.State.SpinCharging)) != 0);
        animator.SetBool(propIsSpringing, !movement.isOnGround && movement.velocity.y > 0 && (movement.state & PlayerCharacterMovement.State.Jumped) == 0);
        animator.SetBool(propIsFreeFalling, !movement.isOnGround && movement.velocity.y < 0 && (movement.state & PlayerCharacterMovement.State.Jumped) == 0);
        animator.SetBool(propIsHurt, (movement.state & PlayerCharacterMovement.State.Pained) != 0);
        animator.SetBool(propIsGliding, (movement.state & PlayerCharacterMovement.State.Gliding) != 0);
        animator.SetFloat(propSpinSpeed, spinSpeed);
    }

    private void LateUpdate()
    {
        if ((movement.state & (PlayerCharacterMovement.State.Rolling | PlayerCharacterMovement.State.Jumped)) == 0) // spinning animations shouldn't normally be tampered with
        {
            Vector3 groundVelocity = movement.groundVelocity;
            Vector3 characterUp = movement.up;
            Vector3 groundForward = transform.forward.AlongPlane(characterUp).normalized;
            Vector3 groundAimForward = player.liveInput.aimDirection.AlongPlane(characterUp).normalized;

            // Turn body towards look direction
            if (movement.isOnGround && groundVelocity.magnitude > 0.2f)
            {
                Vector3 runForward = groundVelocity;

                if (Vector3.Dot(groundForward, runForward) <= 0f)
                {
                    runForward = -runForward;
                }

                Quaternion forwardToVelocity = Quaternion.LookRotation(runForward, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(groundForward, characterUp));

                root.rotation = Quaternion.RotateTowards(lastRootRotation, forwardToVelocity * root.rotation, Time.deltaTime * legTurnDegreesPerSecond);
                torso.rotation = Quaternion.Inverse(forwardToVelocity) * torso.rotation;

                lastRootRotation = root.rotation;
            }

            if ((movement.state & PlayerCharacterMovement.State.Gliding) != 0)
            {
                float tiltAngle = 0f;
                Vector3 groundSide = Vector3.Cross(groundForward, Vector3.up);
                float dot = Vector3.Dot(groundVelocity.normalized, groundAimForward.normalized);

                tiltAngle = Mathf.Acos(Mathf.Clamp(dot, 0f, 1f)) * Mathf.Rad2Deg * -Mathf.Sign(Vector3.Dot(groundSide, groundVelocity - groundAimForward));
                tiltAngle = Mathf.Lerp(0f, tiltAngle, Mathf.Abs(dot / glideTiltSpeedBlend));

                characterUp = Quaternion.Inverse(root.rotation) * characterUp;
                root.rotation = root.rotation * Quaternion.Euler(0f, tiltAngle, 0f);
                characterUp = root.rotation * characterUp;
            }

            // think of this as rotation = originalRotation - forwardRotation + newHeadForwardRotation
            // head - (head.forward, charUp) + (aim, up)
            head.transform.rotation = Quaternion.LookRotation(player.liveInput.aimDirection, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(head.forward.AlongPlane(characterUp), characterUp)) * head.transform.rotation;
        }

        // After animation post-processing, handle stuff attached to the player
        TheFlag holdingFlag = player.holdingFlag;
        if (holdingFlag != null)
        {
            holdingFlag.transform.SetPositionAndRotation(player.flagHoldBone.position - (player.flagHoldBone.rotation * Vector3.up) * 0.4f, player.flagHoldBone.rotation);
        }
    }
}
