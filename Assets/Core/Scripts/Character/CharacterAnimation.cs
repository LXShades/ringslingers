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

        if ((movement.state & PlayerCharacterMovement.State.Rolling) != 0f)
        {
            if (movement.spindashChargeLevel > 0f)
                spinSpeed = movement.spindashChargeLevel * movement.spindashMaxSpeed;
            else
                spinSpeed = movement.velocity.magnitude;
        }

        animator.SetFloat("HorizontalSpeed", groundVelocity.magnitude);
        animator.SetFloat("HorizontalForwardSpeed", groundVelocity.magnitude * forwardSpeedMultiplier);
        animator.SetBool("IsOnGround", movement.isOnGround);
        animator.SetBool("IsRolling", (movement.state & (PlayerCharacterMovement.State.Jumped | PlayerCharacterMovement.State.Rolling)) != 0);
        animator.SetBool("IsSpringing", !movement.isOnGround && movement.velocity.y > 0 && (movement.state & PlayerCharacterMovement.State.Jumped) == 0);
        animator.SetBool("IsFreeFalling", !movement.isOnGround && movement.velocity.y < 0 && (movement.state & PlayerCharacterMovement.State.Jumped) == 0);
        animator.SetBool("IsHurt", (movement.state & PlayerCharacterMovement.State.Pained) != 0);
        animator.SetBool("IsGliding", (movement.state & PlayerCharacterMovement.State.Gliding) != 0);
        animator.SetFloat("SpinSpeed", spinSpeed);
    }

    private void LateUpdate()
    {
        if ((movement.state & (PlayerCharacterMovement.State.Rolling | PlayerCharacterMovement.State.Jumped)) != 0)
        {
            return; // spinning animation shouldn't be tampered with
        }

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
}
