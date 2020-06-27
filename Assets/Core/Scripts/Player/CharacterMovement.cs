using MLAPI.Serialization.Pooled;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Player))]
public class CharacterMovement : SyncedObject
{
    private Player player;
    private CharacterController controller;

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

    [Header("Sounds")]
    public GameSound jumpSound = new GameSound();
    public GameSound thokSound = new GameSound();

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
    /// Speed of the player right now
    /// </summary>
    [HideInInspector] public Vector3 velocity;

    /// <summary>
    /// The direction the player is trying to run in
    /// </summary>
    private Vector3 inputRunDirection;

    /// <summary>
    /// Whether the player is on the ground
    /// </summary>
    [HideInInspector] public bool isOnGround = false;

    public override void FrameStart()
    {
        controller = GetComponent<CharacterController>();
        player = GetComponent<Player>();
    }

    public override void FrameUpdate()
    {
        // Check whether on ground
        isOnGround = DetectOnGround();

        // Point towards relevent direction
        transform.rotation = Quaternion.Euler(0, player.input.horizontalAim, 0);

        // Friction
        ApplyFriction();
        float lastHorizontalSpeed = velocity.Horizontal().magnitude; // this is our new max speed if our max speed was already exceeded

        ApplyRunAcceleration();

        // Gravity
        velocity += new Vector3(0, -1, 0) * (gravity * 35f * Frame.local.deltaTime);

        // Top speed clamp
        ApplyTopSpeedLimit(lastHorizontalSpeed);

        // Stop speed
        if (inputRunDirection.sqrMagnitude == 0 && velocity.Horizontal().magnitude < stopSpeed)
            velocity.SetHorizontal(Vector3.zero);

        // Jump button
        HandleJumpAbilities();

        // Do not slip through the ground
        if (isOnGround)
        {
            velocity.y = Mathf.Max(velocity.y, 0);

            if (velocity.y == 0)
            {
                isOnGround = true;
                state &= ~(State.Jumped | State.Thokked | State.CanceledJump);
            }
        }

        // Perform final movement and collision
        if (debugDisableCollision)
        {
            transform.position += velocity * (35 * Frame.local.deltaTime / GameManager.singleton.fracunitsPerM);
        }
        else
        {
            controller.Move(velocity * (35 * Frame.local.deltaTime / GameManager.singleton.fracunitsPerM));
            Physics.SyncTransforms();
        }
    }

    public bool debugDisableCollision = true;

    private bool DetectOnGround()
    {
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 0.1f, -Vector3.up, 0.199f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.GetComponentInParent<Player>())
                return true;
        }

        return false;
    }

    private void ApplyFriction()
    {
        // Friction
        if (velocity.Horizontal().magnitude > 0 && isOnGround)
            velocity.SetHorizontal(velocity.Horizontal() * Mathf.Pow(friction, Frame.local.deltaTime * 35f));
    }

    private void ApplyRunAcceleration()
    {
        inputRunDirection = transform.forward.Horizontal().normalized * player.input.moveVerticalAxis + transform.right.Horizontal().normalized * player.input.moveHorizontalAxis;

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
            velocity.SetHorizontal(velocity.Horizontal() * lastHorizontalSpeed / speedToClamp);
    }

    private void HandleJumpAbilities()
    {
        if (player.input.btnJump)
        {
            if (isOnGround && !state.HasFlag(State.Jumped) && !player.lastInput.btnJump)
            {
                // Jump
                velocity.y = jumpSpeed * jumpFactor;
                GameSounds.PlaySound(gameObject, jumpSound);
                state |= State.Jumped;
            }
            else if (!state.HasFlag(State.Thokked) && state.HasFlag(State.Jumped) && !player.lastInput.btnJump)
            {
                // Thok
                velocity.SetHorizontal(player.aimForward.Horizontal().normalized * actionSpeed);
                GameSounds.PlaySound(gameObject, thokSound);
                state |= State.Thokked;
            }
        }
        else if (state.HasFlag(State.Jumped) && !state.HasFlag(State.CanceledJump))
        {
            // Cancel jump
            state |= State.CanceledJump;

            if (velocity.y > 0)
                velocity.y /= 2f;
        }
    }

    public void SpringUp(float force, Vector3 direction)
    {
        state &= ~(State.Jumped | State.Thokked | State.CanceledJump);
        velocity = velocity - direction * (Vector3.Dot(direction, velocity) / Vector3.Dot(direction, direction)) + force * direction;
    }

    #region Networking
    public override void ReadSyncer(System.IO.Stream stream)
    {
        Vector3 originalPosition = transform.position;
        Vector3 originalVelocity = velocity;

        using (System.IO.BinaryReader reader = new System.IO.BinaryReader(stream, System.Text.Encoding.ASCII, true))
        {
            Vector3 position;
            position.x = reader.ReadSingle();
            position.y = reader.ReadSingle();
            position.z = reader.ReadSingle();
            velocity.x = reader.ReadSingle();
            velocity.y = reader.ReadSingle();
            velocity.z = reader.ReadSingle();
            transform.position = position;
        }

        if (player.playerId == Netplay.singleton.localPlayerId)
            Debug.Log($"Received sync {Frame.local.time}/{Time.unscaledTime}! Difference: {transform.position - originalPosition}, {velocity - originalVelocity}");
    }

    public override void WriteSyncer(System.IO.Stream stream)
    {
        using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(transform.position.x);
            writer.Write(transform.position.y);
            writer.Write(transform.position.z);
            writer.Write(velocity.x);
            writer.Write(velocity.y);
            writer.Write(velocity.z);
        }
    }
    #endregion
}