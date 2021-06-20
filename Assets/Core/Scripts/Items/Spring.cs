using UnityEngine;

public class Spring : MonoBehaviour, IMovementCollisions
{
    [Header("Spring properties")]
    // red: 32, yellow: 20, blue: 11
    public float springForce = (32 * 35);

    public float maxSpeedDotForSpring = 0.9f;

    public bool springAbsolutely = false;

    public float absoluteStartHeight = 0.5f;

    [Header("Sounds")]
    public GameSound springSound = new GameSound();

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void OnMovementCollidedBy(Movement source, bool isRealtime)
    {
        CharacterMovement movement = source as CharacterMovement;

        if (movement && Vector3.Dot(movement.velocity, transform.up) < maxSpeedDotForSpring * springForce)
        {
            if (springAbsolutely)
                movement.transform.position = transform.position + transform.up * absoluteStartHeight; // line up player with spring centre

            movement.SpringUp(springForce, transform.up, springAbsolutely);

            if (isRealtime)
            {
                animator.SetTrigger("DoSpring");

                GameSounds.PlaySound(gameObject, springSound);
            }
        }
    }
}
