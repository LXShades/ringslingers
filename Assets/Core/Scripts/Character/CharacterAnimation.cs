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
    private Vector3 lastVelocity;

    private float smoothGlideTilt = 0f;
    private float smoothGlideTiltVelocity = 0f;

    private AnimatorFloat propHorizontalSpeed;
    private AnimatorFloat propHorizontalForwardSpeed;
    private AnimatorBool propIsOnGround;
    private AnimatorBool propIsRolling;
    private AnimatorBool propIsSpringing;
    private AnimatorBool propIsFreeFalling;
    private AnimatorBool propIsHurt;
    private AnimatorBool propIsGliding;
    private AnimatorBool propIsFlying;
    private AnimatorFloat propSpinSpeed;

    private void Start()
    {
        movement = GetComponentInParent<PlayerCharacterMovement>();
        player = GetComponentInParent<Character>();
        animator = GetComponentInParent<Animator>();

        propHorizontalSpeed = new AnimatorFloat(animator, "HorizontalSpeed");
        propHorizontalForwardSpeed = new AnimatorFloat(animator, "HorizontalForwardSpeed");
        propIsOnGround = new AnimatorBool(animator, "IsOnGround");
        propIsRolling = new AnimatorBool(animator, "IsRolling");
        propIsSpringing = new AnimatorBool(animator, "IsSpringing");
        propIsFreeFalling = new AnimatorBool(animator, "IsFreeFalling");
        propIsHurt = new AnimatorBool(animator, "IsHurt");
        propIsGliding = new AnimatorBool(animator, "IsGliding");
        propIsFlying = new AnimatorBool(animator, "IsFlying");
        propSpinSpeed = new AnimatorFloat(animator, "SpinSpeed");
    }

    private void Update()
    {
        Vector3 groundVelocity = movement.groundVelocity;
        float forwardSpeedMultiplier = Vector3.Dot(transform.forward.AlongPlane(movement.gravityDirection), groundVelocity) <= 0f ? -1 : 1;
        float spinSpeed = 15f;

        if (movement.IsAnyState(CharacterMovementState.Rolling, CharacterMovementState.SpinCharging))
            spinSpeed = Mathf.Max(movement.velocity.magnitude, movement.spindashChargeLevel * movement.spindashMaxSpeed);

        float upwardVelocity = -movement.velocity.AlongAxis(movement.gravityDirection);
        propHorizontalSpeed.value = groundVelocity.magnitude;
        propHorizontalForwardSpeed.value = groundVelocity.magnitude * forwardSpeedMultiplier;
        propIsOnGround.value = movement.isOnGround;
        propIsRolling.value = movement.isSpinblading;
        propIsSpringing.value = !movement.isOnGround && upwardVelocity > 0 && movement.state == CharacterMovementState.None;
        propIsFreeFalling.value = !movement.isOnGround && upwardVelocity < 0 && movement.state == CharacterMovementState.None;
        propIsHurt.value = movement.state == CharacterMovementState.Pained;
        propIsGliding.value = movement.state == CharacterMovementState.Gliding;
        propIsFlying.value = movement.state == CharacterMovementState.Flying;
        propSpinSpeed.value = spinSpeed;
    }

    private void LateUpdate()
    {
        float glideTilt = 0f;

        if (!movement.isSpinblading) // spinning animations shouldn't normally be tampered with
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

            if (movement.state == CharacterMovementState.Gliding)
            {
                characterUp = Quaternion.Inverse(root.rotation) * characterUp;

                // Glide tilt
                glideTilt = Vector3.Angle(lastVelocity.AlongPlane(movement.gravityDirection).normalized, movement.velocity.AlongPlane(movement.gravityDirection).normalized) * Mathf.Sign(Vector3.Cross(lastVelocity, movement.velocity).y) / Time.deltaTime;

                root.rotation *= Quaternion.AngleAxis(smoothGlideTilt * glideTiltWeight, root.forward);

                characterUp = root.rotation * characterUp;
            }

            // think of this as rotation = originalRotation - forwardRotation + newHeadForwardRotation
            // head - (head.forward, charUp) + (aim, up)
            head.rotation = Quaternion.LookRotation(player.liveInput.aimDirection, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(head.forward.AlongPlane(characterUp), characterUp)) * head.transform.rotation;
        }

        smoothGlideTilt = Mathf.SmoothDamp(smoothGlideTilt, glideTilt, ref smoothGlideTiltVelocity, glideTiltDamp);

        // After animation post-processing, handle stuff attached to the player
        System.Collections.Generic.List<Carryable> itemsCarriedByPlayer = Carryable.GetAllCarriedByPlayer(player);
        foreach (Carryable carryable in itemsCarriedByPlayer) // we haven't got handling for multiple carried things yet whee
        {
            carryable.transform.SetPositionAndRotation(player.flagHoldBone.position - (player.flagHoldBone.rotation * carryable.localHandCarrySocketOffset), player.flagHoldBone.rotation);
        }

        lastVelocity = movement.velocity;
    }
}
