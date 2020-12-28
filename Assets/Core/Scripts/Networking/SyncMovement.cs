using Mirror;
using UnityEngine;

[RequireComponent(typeof(Movement))]
public class SyncMovement : NetworkBehaviour
{
    public struct SyncMovementUpdate
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
    }

    [Range(1, 10)]
    public float updatesPerSecond = 1f;

    [HideInInspector] public Movement movement;

    private float lastUpdateTime = float.MinValue;

    private void Awake()
    {
        movement = GetComponent<Movement>();
    }

    private void Update()
    {
        if (NetworkServer.active)
        {
            if (Time.unscaledTime - lastUpdateTime > 1f / updatesPerSecond)
            {
                RpcMovementUpdate(new SyncMovementUpdate()
                {
                    position = transform.position,
                    rotation = transform.rotation,
                    velocity = movement.velocity
                });

                lastUpdateTime = Time.unscaledTime;
            }
        }
    }

    [ClientRpc(channel = Channels.DefaultUnreliable)]
    private void RpcMovementUpdate(SyncMovementUpdate update)
    {
        transform.position = update.position;
        transform.rotation = update.rotation;
        movement.velocity = update.velocity;
    }
}
