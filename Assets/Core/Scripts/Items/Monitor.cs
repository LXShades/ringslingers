using Mirror;
using UnityEngine;

public class Monitor : NetworkBehaviour, IMovementCollisions
{
    [Header("Destroying")]
    public Renderer monitorRenderer;
    public Collider monitorCollider;

    [Header("Looking")]
    public Transform monitorHead;
    public float maxVerticalLookAngle = 30f;
    public float lookSmoothTime = 0.1f;
    public float centreHeight = 0.5f;

    private Vector3 lookVelocity;

    private bool isDestroyed
    {
        set
        {
            monitorCollider.enabled = value;
            monitorRenderer.enabled = value;
        }
        get => monitorCollider.enabled;
    }

    public void Update()
    {
        if (monitorHead && GameManager.singleton.camera != null)
        {
            Vector3 targetLookDirection = (GameManager.singleton.camera.transform.position - new Vector3(0, centreHeight, 0) - monitorHead.position).normalized;
            float clampY = Mathf.Sin(maxVerticalLookAngle * Mathf.Deg2Rad);

            // srqt x*x + z*z + y*y = 1
            // sqrt x*x + z*z = 1 - y*y
            // x*x + z*z = (1 - y*y)2
            targetLookDirection.y = Mathf.Clamp(targetLookDirection.y, -clampY, clampY);
            float horizontalMult = Mathf.Sqrt(1f - Mathf.Abs(targetLookDirection.y)) / targetLookDirection.Horizontal().magnitude;
            targetLookDirection.x *= horizontalMult;
            targetLookDirection.z *= horizontalMult;

            monitorHead.transform.forward = Vector3.SmoothDamp(monitorHead.transform.forward, targetLookDirection, ref lookVelocity, lookSmoothTime);
        }
    }

    public void OnMovementCollidedBy(Movement source, bool isReconciliation)
    {
        if (source is CharacterMovement character)
        {
            if (character.velocity.y <= -1.0f)
            {
                character.velocity.y = -character.velocity.y;
                isDestroyed = true;
            }
        }
    }
}
