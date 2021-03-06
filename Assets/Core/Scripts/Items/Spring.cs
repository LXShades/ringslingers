using UnityEngine;

public class Spring : MonoBehaviour, IMovementCollisions
{
    [Header("Spring properties")]
    // red: 32, yellow: 20, blue: 11
    public float springForce = (32 * 35);

    public float maxSpeedDotForSpring = 0.9f;

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

        if (movement && Vector3.Dot(movement.velocity, transform.up) < maxSpeedDotForSpring * springForce)
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
