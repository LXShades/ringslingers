using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Monitor : NetworkBehaviour, IMovementCollisionCallbacks
{
    public Collider mainCollider;

    [Header("Looking")]
    public Transform monitorHead;
    public float maxVerticalLookAngle = 30f;
    public float lookSmoothTime = 0.1f;
    public float centreHeight = 0.5f;

    [Header("Pop")]
    public GameSound popSound;
    public UnityEvent<Character> onLocalPopped;

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

    private RespawnableItem respawnable;

    private void Start()
    {
        respawnable = GetComponent<RespawnableItem>();

        if (respawnable)
        {
            respawnable.onDespawn += OnDespawn;
            respawnable.onRespawn += OnRespawn;
        }
    }

    private void Update()
    {
        if (monitorHead && GameManager.singleton.camera != null)
        {
            Vector3 targetLookDirection = (GameManager.singleton.camera.transform.position - (monitorHead.position + transform.up * centreHeight)).normalized;
            float clampY = Mathf.Sin(maxVerticalLookAngle * Mathf.Deg2Rad);

            Vector3 lookHorizontalness = targetLookDirection.AlongPlane(transform.up);
            float lookUpness = Mathf.Clamp(targetLookDirection.AlongAxis(transform.up), -clampY, clampY);
            float horizontalMult = Mathf.Sqrt(1f - Mathf.Abs(lookUpness)) / lookHorizontalness.magnitude;
            targetLookDirection.SetAlongPlane(transform.up, lookHorizontalness * horizontalMult);
            targetLookDirection.SetAlongAxis(transform.up, lookUpness);

            monitorHead.transform.rotation = Quaternion.LookRotation(Vector3.SmoothDamp(monitorHead.transform.forward, targetLookDirection, ref lookVelocity, lookSmoothTime), transform.up);
        }
    }

    private void OnRespawn()
    {
        isDestroyed = false;
    }

    private void OnDespawn()
    {
        isDestroyed = true;
        GameSounds.PlaySound(gameObject, popSound);
    }

    public bool ShouldBlockMovement(Movement source, in RaycastHit hit)
    {
        if (source is PlayerCharacterMovement character && (character.isSpinblading || character.baseState == CharacterMovementState.Gliding))
            return Vector3.Dot(hit.normal, transform.up) >= 0.95f; // being jumped on from the top means we should block, otherwise let the character through

        return true;
    }

    public void OnMovementCollidedBy(Movement source, TickInfo tickInfo)
    {
        if (source is PlayerCharacterMovement character && (character.isSpinblading || character.baseState == CharacterMovementState.Gliding))
        {
            // bounce off
            float upwardVelocity = -character.velocity.AlongAxis(character.gravityDirection);
            if (upwardVelocity <= -1.0f)
                character.velocity.SetAlongAxis(character.gravityDirection, upwardVelocity);

            // pop
            if (respawnable.isSpawned && tickInfo.isFullTick)
            {
                onLocalPopped?.Invoke(source.GetComponent<Character>());

                if (respawnable && isServer)
                    respawnable.Despawn();
            }
        }
    }
}
