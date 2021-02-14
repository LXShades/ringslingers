using UnityEngine;

public class Spring : MonoBehaviour, IMovementCollisions
{
    [Header("Spring properties")]
    // red: 32, yellow: 20, blue: 11
    public float springForce = (32 * 35);

    public bool springAbsolutely = false;

    [Header("Sounds")]
    public GameSound springSound = new GameSound();

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void OnMovementCollidedBy(Movement source, bool isReconciliation)
    {
        CharacterMovement movement = source as CharacterMovement;

        if (movement && (movement.velocity.AlongAxis(transform.up) < springForce * 0.7f || springAbsolutely))
        {
            movement.SpringUp(springForce, transform.up, springAbsolutely);

            if (!isReconciliation)
            {
                animator.SetTrigger("DoSpring");

                GameSounds.PlaySound(gameObject, springSound);
            }
        }
    }
}
