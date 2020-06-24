using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Player))]
public class CharacterMovement : SyncedObject
{
    private Player player;
    private CharacterController controller;
    private new PlayerCamera camera;

    [Header("Movement (all FRACUNITS)")]

    public float accelStart = 96;
    public float acceleration = 40;
    public float thrustFactor = 5;
    public float topSpeed = 36;

    public float friction = 0.90625f;
    public float stopSpeed = (1f / 64f);

    public float jumpSpeed = (39f / 4f);
    public float jumpFactor = 1;
    public float gravity = 0.5f;

    public float airAccelerationMultiplier = 0.25f;

    [Header("Abilities")]
    public float actionSpeed = 60;

    [Header("Scaling")]
    public float fracunitsPerM = 64;

    private Vector3 velocity;

    // States
    [Flags]
    enum State
    {
        Jumped   = 1,
        Rolling  = 2,
        Thokked  = 4,
        CanceledJump = 8
    };
    private State state;

    /// <summary>
    /// The direction the player is trying to run in
    /// </summary>
    private Vector3 inputRunDirection;

    private bool isOnGround = false;

    public override void FrameStart()
    {
        camera = FindObjectOfType<PlayerCamera>();
        controller = FindObjectOfType<CharacterController>();
        player = GetComponent<Player>();
    }

    public override void FrameUpdate()
    {
        float lastHorizontalSpeed = velocity.Horizontal().magnitude;

        // Check whether on ground
        isOnGround = DetectOnGround();

        if (isOnGround)
        {
            velocity.y = Mathf.Max(velocity.y, 0);

            if (velocity.y == 0)
            {
                isOnGround = true;
                state &= ~(State.Jumped | State.Thokked | State.CanceledJump);
            }
        }

        // Move based on where we're looking, EZ
        ApplyFriction();

        ApplyRunAcceleration();

        // Gravity
        velocity += new Vector3(0, -1, 0) * (gravity * 35f * Frame.local.deltaTime);

        // Top speed clamp
        ApplyTopSpeedLimit(lastHorizontalSpeed);

        // Stop speed
        if (inputRunDirection.sqrMagnitude == 0 && velocity.Horizontal().magnitude < stopSpeed)
        {
            velocity.SetHorizontal(Vector3.zero);
        }

        // Perform final movement and collision
        controller.Move(velocity * (35 * Frame.local.deltaTime / fracunitsPerM));

        // Jump button
        HandleJumpAbilities();

        // Point towards relevent direction
        transform.rotation = Quaternion.Euler(0, camera.horizontalAngle, 0);
    }

    private bool DetectOnGround()
    {
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 0.1f, -Vector3.up, 0.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.GetComponentInParent<Player>())
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyFriction()
    {
        // Friction
        if (velocity.magnitude > 0 && isOnGround)
        {
            velocity.SetHorizontal(velocity.Horizontal() * Mathf.Pow(friction, Frame.local.deltaTime * 35f));
        }
    }

    private void ApplyRunAcceleration()
    {
        inputRunDirection = camera.transform.forward.Horizontal().normalized * player.input.moveVerticalAxis + camera.transform.right.Horizontal().normalized * player.input.moveHorizontalAxis;

        if (inputRunDirection.magnitude > 1)
            inputRunDirection.Normalize();

        float speed = velocity.Horizontal().magnitude; // todo: use rmomentum
        float currentAcceleration = accelStart + speed * acceleration * Mathf.Pow(1, Frame.local.deltaTime * 35f); // divide by scale in the real game

        if (!isOnGround)
            currentAcceleration *= airAccelerationMultiplier;

        velocity += inputRunDirection * (50 * thrustFactor * currentAcceleration * 35f / 65536f * Frame.local.deltaTime);
    }

    private void ApplyTopSpeedLimit(float lastHorizontalSpeed)
    {
        float speedToClamp = velocity.Horizontal().magnitude;

        if (speedToClamp > topSpeed && speedToClamp > lastHorizontalSpeed)
        {
            velocity.SetHorizontal(velocity.Horizontal() * lastHorizontalSpeed / velocity.magnitude);
        }
    }

    private void HandleJumpAbilities()
    {
        if (player.input.btnJump)
        {
            if (isOnGround && !state.HasFlag(State.Jumped) && !player.lastInput.btnJump)
            {
                // Jump
                velocity.y = jumpSpeed * jumpFactor;
                state |= State.Jumped;
            }
            else if (!state.HasFlag(State.Thokked) && !player.lastInput.btnJump)
            {
                // Thok
                velocity.SetHorizontal(camera.transform.forward.Horizontal().normalized * actionSpeed);
                state |= State.Thokked;
            }
        }
        else if (state.HasFlag(State.Jumped) && !state.HasFlag(State.CanceledJump))
        {
            // Cancel jump
            state |= State.CanceledJump;

            if (velocity.y > 0)
            {
                velocity.y /= 2f;
            }
        }
    }
}