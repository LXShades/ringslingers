using UnityEngine;

public class Spring : MonoBehaviour, IMovementCollisionCallbacks
{
    [Header("Spring properties")]
    // red: 32, yellow: 20, blue: 11
    public float springForce = (32 * 35);

    public float maxSpeedDotForSpring = 0.9f;

    public bool springAbsolutely = false;

    public float absoluteStartHeight = 0.5f;

    [Header("Sounds")]
    public GameSound springSound = new GameSound();

    [Header("Debug")]
    public float _dbgGravityForce = 9.57f;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void OnMovementCollidedBy(Movement source, TickInfo tickInfo)
    {
        PlayerCharacterMovement movement = source as PlayerCharacterMovement;

        if (movement && Vector3.Dot(movement.velocity, transform.up) < maxSpeedDotForSpring * springForce)
        {
            if (springAbsolutely)
                movement.transform.position = transform.position + transform.up * absoluteStartHeight; // line up player with spring centre

            movement.SpringUp(springForce, transform.up, springAbsolutely);

            if (tickInfo.isConfirmingForward)
            {
                animator.SetTrigger("DoSpring");

                GameSounds.PlaySound(gameObject, springSound);
            }
        }
    }

    public bool ShouldBlockMovement(Movement source, in RaycastHit hit) => true;

    /// <summary>
    /// Previews spring forces
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        float delta = 0.033f;
        Vector3 position = transform.position + transform.up * absoluteStartHeight;
        Vector3 velocity = transform.up * springForce;
        GravityVolume nearestGravVol = null;
        float nearestGravVolDist = float.MaxValue;
        Vector3 lastDrawnPoint = transform.position;

        Gizmos.color = Color.blue;

        foreach (GravityVolume gravVol in FindObjectsOfType<GravityVolume>())
        {
            float dist = Vector3.Distance(gravVol.transform.position, transform.position);
            if (dist < nearestGravVolDist)
            {
                nearestGravVolDist = dist;
                nearestGravVol = gravVol;
            }
        }

        for (float t = 0; t < 2.25f; t += delta)
        {
            position += velocity * delta;

            if (nearestGravVol != null && Vector3.Distance(position, nearestGravVol.transform.position) <= nearestGravVol.maxRadius)
                velocity += (nearestGravVol.transform.position - position).normalized * (_dbgGravityForce * delta);
            else
                velocity += new Vector3(0f, -_dbgGravityForce * delta, 0f);

            Gizmos.DrawLine(lastDrawnPoint, position);
            lastDrawnPoint = position;
        }

    }
}
