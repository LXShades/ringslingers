using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    private CharacterMovement movement;
    private Player player;
    private Animator animation;

    [Header("Body parts")]
    public Transform root;
    public Transform torso;
    public Transform head;

    private void Start()
    {
        movement = GetComponentInParent<CharacterMovement>();
        player = GetComponentInParent<Player>();
        animation = GetComponentInParent<Animator>();
    }

    private void LateUpdate()
    {
        float forwardSpeedMultiplier = 1;
        Vector3 groundVelocity = movement.groundVelocity;

        // Turn body towards look direction
        if (movement.isOnGround && groundVelocity.magnitude > 0.2f)
        {
            Vector3 runForward = groundVelocity;

            if (Vector3.Dot(transform.forward.Horizontal(), runForward) <= 0f)
            {
                runForward = -runForward;
                forwardSpeedMultiplier = -1;
            }

            Quaternion forwardToVelocity = Quaternion.LookRotation(runForward) * Quaternion.Inverse(Quaternion.LookRotation(transform.forward.Horizontal()));

            root.rotation = forwardToVelocity * root.transform.rotation;
            torso.rotation = Quaternion.Inverse(forwardToVelocity) * torso.rotation;
        }

        head.transform.rotation = Quaternion.LookRotation(player.input.aimDirection) * Quaternion.Inverse(Quaternion.LookRotation(torso.forward.Horizontal())) * head.transform.rotation;

        animation.SetFloat("HorizontalSpeed", groundVelocity.magnitude);
        animation.SetFloat("HorizontalForwardSpeed", groundVelocity.magnitude * forwardSpeedMultiplier);
        animation.SetBool("IsOnGround", movement.isOnGround);
        animation.SetBool("IsRolling", !movement.isOnGround && (movement.state & (CharacterMovement.State.Jumped | CharacterMovement.State.Rolling)) != 0);
        animation.SetBool("IsSpringing", !movement.isOnGround && movement.velocity.y > 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsFreeFalling", !movement.isOnGround && movement.velocity.y < 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsHurt", (movement.state & CharacterMovement.State.Pained) != 0);
    }
}
