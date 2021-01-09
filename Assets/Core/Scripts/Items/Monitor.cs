using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Monitor : NetworkBehaviour, IMovementCollisions
{
    public Collider mainCollider;

    [Header("Looking")]
    public Transform monitorHead;
    public float maxVerticalLookAngle = 30f;
    public float lookSmoothTime = 0.1f;
    public float centreHeight = 0.5f;

    [Header("Pop")]
    public GameSound popSound;
    public UnityEvent<Player> onLocalPopped;

    private Vector3 lookVelocity;

    private bool isDestroyed
    {
        set
        {
            monitorHead.gameObject.SetActive(!value);
            mainCollider.enabled = !value;
        }
        get => !monitorHead.gameObject.activeSelf;
    }

    public void Update()
    {
        if (monitorHead && GameManager.singleton.camera != null)
        {
            Vector3 targetLookDirection = (GameManager.singleton.camera.transform.position - new Vector3(0, centreHeight, 0) - monitorHead.position).normalized;
            float clampY = Mathf.Sin(maxVerticalLookAngle * Mathf.Deg2Rad);

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
            // bounce off
            if (character.velocity.y <= -1.0f)
            {
                character.velocity.y = -character.velocity.y;
            }

            // pop
            if (!isReconciliation)
            {
                isDestroyed = true;
                GameSounds.PlaySound(gameObject, popSound);

                onLocalPopped?.Invoke(source.GetComponent<Player>());
            }
        }
    }
}
