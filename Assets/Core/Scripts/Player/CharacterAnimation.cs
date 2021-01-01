using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    private CharacterMovement movement;
    private Animator animation;

    private void Start()
    {
        movement = GetComponentInParent<CharacterMovement>();
        animation = GetComponentInParent<Animator>();
    }

    private void LateUpdate()
    {
        animation.SetFloat("HorizontalSpeed", movement.velocity.Horizontal().magnitude);
        animation.SetBool("IsOnGround", movement.isOnGround);
        animation.SetBool("IsRolling", !movement.isOnGround && (movement.state & (CharacterMovement.State.Jumped | CharacterMovement.State.Rolling)) != 0);
        animation.SetBool("IsSpringing", !movement.isOnGround && movement.velocity.y > 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsFreeFalling", !movement.isOnGround && movement.velocity.y < 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsHurt", (movement.state & CharacterMovement.State.Pained) != 0);
    }
}
