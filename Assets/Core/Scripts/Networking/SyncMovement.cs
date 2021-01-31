﻿using Mirror;
using UnityEngine;

[RequireComponent(typeof(Movement))]
public class SyncMovement : NetworkBehaviour
{
    public struct SyncMovementUpdate
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
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
                    localPosition = transform.localPosition,
                    localRotation = transform.localRotation,
                    velocity = movement.velocity
                });

                lastUpdateTime = Time.unscaledTime;
            }
        }
    }

    [ClientRpc(channel = Channels.DefaultUnreliable)]
    private void RpcMovementUpdate(SyncMovementUpdate update)
    {
        transform.localPosition = update.localPosition;
        transform.localRotation = update.localRotation;
        movement.velocity = update.velocity;
    }
}
