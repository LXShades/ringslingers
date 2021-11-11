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
    public float glideTiltWeight = 0.3f;
    public float glideTiltDamp = 0.1f;

    private Quaternion lastRootRotation = Quaternion.identity;
    private Vector3 lastCharacterUp = Vector3.up;
    private Vector3 lastVelocity;

    private float smoothGlideTilt = 0f;
    private float smoothGlideTiltVelocity = 0f;

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
        float glideTilt = 0f;

        if ((movement.state & (PlayerCharacterMovement.State.Rolling | PlayerCharacterMovement.State.Jumped)) == 0 || (movement.state & PlayerCharacterMovement.State.Gliding) != 0) // spinning animations shouldn't normally be tampered with
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
                characterUp = Quaternion.Inverse(root.rotation) * characterUp;

                // Glide tilt
                glideTilt = Vector3.Angle(lastVelocity.Horizontal().normalized, movement.velocity.Horizontal().normalized) * Mathf.Sign(Vector3.Cross(lastVelocity, movement.velocity).y) / Time.deltaTime;

                root.rotation *= Quaternion.AngleAxis(smoothGlideTilt * glideTiltWeight, root.forward);

                characterUp = root.rotation * characterUp;
            }

            // think of this as rotation = originalRotation - forwardRotation + newHeadForwardRotation
            // head - (head.forward, charUp) + (aim, up)
            head.rotation = Quaternion.LookRotation(player.liveInput.aimDirection, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(head.forward.AlongPlane(characterUp), characterUp)) * head.transform.rotation;
        }

        smoothGlideTilt = Mathf.SmoothDamp(smoothGlideTilt, glideTilt, ref smoothGlideTiltVelocity, glideTiltDamp);

        // After animation post-processing, handle stuff attached to the player
        TheFlag holdingFlag = player.holdingFlag;
        if (holdingFlag != null)
            holdingFlag.transform.SetPositionAndRotation(player.flagHoldBone.position - (player.flagHoldBone.rotation * Vector3.up) * 0.4f, player.flagHoldBone.rotation);

        lastVelocity = movement.velocity;
    }
}
