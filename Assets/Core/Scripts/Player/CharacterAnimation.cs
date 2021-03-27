using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    private CharacterMovement movement;
    private Player player;
    private new Animator animation;

    [Header("Body parts")]
    public Transform root;
    public Transform torso;
    public Transform head;

    [Header("Settings")]
    public float legTurnDegreesPerSecond = 360f;
    public float fallTiltDegreesPerSecond = 50f;
    public float fallTiltMaxDegrees = 20f;

    private Quaternion lastRootRotation = Quaternion.identity;
    private Vector3 lastCharacterUp = Vector3.up;

    private void Start()
    {
        movement = GetComponentInParent<CharacterMovement>();
        player = GetComponentInParent<Player>();
        animation = GetComponentInParent<Animator>();
    }

    private void Update()
    {
        Vector3 groundVelocity = movement.groundVelocity;
        float forwardSpeedMultiplier = Vector3.Dot(transform.forward.Horizontal(), groundVelocity) <= 0f ? -1 : 1;

        animation.SetFloat("HorizontalSpeed", groundVelocity.magnitude);
        animation.SetFloat("HorizontalForwardSpeed", groundVelocity.magnitude * forwardSpeedMultiplier);
        animation.SetBool("IsOnGround", movement.isOnGround);
        animation.SetBool("IsRolling", !movement.isOnGround && (movement.state & (CharacterMovement.State.Jumped | CharacterMovement.State.Rolling)) != 0);
        animation.SetBool("IsSpringing", !movement.isOnGround && movement.velocity.y > 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsFreeFalling", !movement.isOnGround && movement.velocity.y < 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsHurt", (movement.state & CharacterMovement.State.Pained) != 0);
        animation.SetBool("IsGliding", (movement.state & CharacterMovement.State.Gliding) != 0);
    }

    private void LateUpdate()
    {
        Vector3 groundVelocity = movement.groundVelocity;
        Vector3 characterUp = movement.up;
        Vector3 groundForward = transform.forward.AlongPlane(characterUp).normalized;

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

        // think of this as rotation = originalRotation - forwardRotation + newHeadForwardRotation
        // head - (head.forward, charUp) + (aim, up)
        head.transform.rotation = Quaternion.LookRotation(player.input.aimDirection, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(head.forward.AlongPlane(characterUp), characterUp)) * head.transform.rotation;

        if ((movement.state & CharacterMovement.State.Gliding) != 0)
        {
            float tiltAngle = 0f;

            if (groundVelocity.sqrMagnitude > 1f)
            {
                Vector3 groundSide = Vector3.Cross(groundForward, Vector3.up);
                tiltAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(groundVelocity.normalized, groundForward.normalized), -0.9999f, 0.9999f)) * Mathf.Rad2Deg * -Mathf.Sign(Vector3.Dot(groundSide, groundVelocity - groundForward));
            }

            root.rotation = root.rotation * Quaternion.Euler(player.input.verticalAim, tiltAngle, 0f);
        }
    }
}
