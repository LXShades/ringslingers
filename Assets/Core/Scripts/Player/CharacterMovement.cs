﻿using MLAPI.Serialization.Pooled;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Xml;
using UnityEngine;

[RequireComponent(typeof(Movement), typeof(Player))]
public class CharacterMovement : WorldObjectComponent
{
    private Player player;
    private Movement move;

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
    public enum State
    {
        Jumped   = 1,
        Rolling  = 2,
        Thokked  = 4,
        CanceledJump = 8,
        Pained = 16
    };
    public State state
    {
        get; private set;
    }

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

    [Serializable]
    private class TimePosition
    {
        public float time;
        public Vector3 position;
        public Vector3 velocity;
        public InputCmds inputs;
        public State state;

        public TimePosition(CharacterMovement source)
        {
            time = 0;
            position = source.transform.position;
            velocity = source.velocity;
            inputs = source.player.input;
            state = source.state;
        }

        public void To(CharacterMovement target)
        {
            target.transform.position = position;
            target.velocity = velocity;
            target.player.input = inputs;
            target.state = state;
        }
    }

    private float maxMovementHistory = 1; // in seconds
    private List<TimePosition> movementHistory = new List<TimePosition>();

    private bool isResimmingMovement = false;

    public override void FrameStart()
    {
        player = GetComponent<Player>();
        move = GetComponent<Movement>();
    }

    public override void FrameUpdate(float deltaTime)
    {
        if (!isResimmingMovement)
        {
            // Update position/reconciliation etc
            HandleServerMovement();
        }

        // Check whether on ground
        isOnGround = DetectOnGround();

        // Point towards relevent direction
        transform.rotation = Quaternion.Euler(0, player.input.horizontalAim, 0);

        // Add/remove states depending on whether isOnGround
        if (isOnGround)
        {
            if (state.HasFlag(State.Pained))
                player.StartInvincibilityTime();

            state &= ~State.Pained;
        }

        // Friction
        ApplyFriction(deltaTime);
        float lastHorizontalSpeed = velocity.Horizontal().magnitude; // this is our new max speed if our max speed was already exceeded

        ApplyRunAcceleration(deltaTime);

        // Gravity
        velocity += new Vector3(0, -1, 0) * (gravity * 35f * 35f / GameManager.singleton.fracunitsPerM * deltaTime);

        // Top speed clamp
        ApplyTopSpeedLimit(lastHorizontalSpeed);

        // Stop speed
        if (inputRunDirection.sqrMagnitude == 0 && velocity.Horizontal().magnitude < stopSpeed / GameManager.singleton.fracunitsPerM * 35f)
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
            transform.position += velocity * deltaTime;
        }
        else
        {
            RaycastHit hit;
            move.Move(velocity * deltaTime, out hit);
        }
    }

    public bool debugDisableCollision = true;

    private bool DetectOnGround()
    {
        if (debugDisableCollision)
            return transform.position.y <= 0;

        RaycastHit[] hits = new RaycastHit[10];
        int numHits = World.live.physics.Raycast(transform.position + Vector3.up * 0.1f, -Vector3.up.normalized, hits, 0.199f, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < numHits; i++)
        {
            if (!hits[i].collider.GetComponentInParent<Player>())
                return true;
        }

        return false;
    }

    private void ApplyFriction(float deltaTime)
    {
        // Friction
        if (velocity.Horizontal().magnitude > 0 && isOnGround)
            velocity.SetHorizontal(velocity.Horizontal() * Mathf.Pow(friction, deltaTime * 35f));
    }

    private void ApplyRunAcceleration(float deltaTime)
    {
        if (state.HasFlag(State.Pained))
            return; // cannot accelerate while in pain

        inputRunDirection = transform.forward.Horizontal().normalized * player.input.moveVerticalAxis + transform.right.Horizontal().normalized * player.input.moveHorizontalAxis;

        if (inputRunDirection.magnitude > 1)
            inputRunDirection.Normalize();

        velocity *= GameManager.singleton.fracunitsPerM / 35f;
        float speed = velocity.Horizontal().magnitude; // todo: use rmomentum
        float currentAcceleration = accelStart + speed * acceleration; // divide by scale in the real game

        if (!isOnGround)
            currentAcceleration *= airAccelerationMultiplier;

        velocity += inputRunDirection * (50 * thrustFactor * currentAcceleration / 65536f * deltaTime * 35f);
        velocity /= GameManager.singleton.fracunitsPerM / 35f;
    }

    private void ApplyTopSpeedLimit(float lastHorizontalSpeed)
    {
        float speedToClamp = velocity.Horizontal().magnitude;

        if (speedToClamp > topSpeed / GameManager.singleton.fracunitsPerM * 35f && speedToClamp > lastHorizontalSpeed)
            velocity.SetHorizontal(velocity.Horizontal() * (Mathf.Max(lastHorizontalSpeed, topSpeed / GameManager.singleton.fracunitsPerM * 35f) / speedToClamp));
    }

    public void ApplyHitKnockback(Vector3 force)
    {
        state |= State.Pained;
        velocity = force;
    }

    private void HandleJumpAbilities()
    {
        if (state.HasFlag(State.Pained))
            return;

        if (player.input.btnJump)
        {
            if (isOnGround && !state.HasFlag(State.Jumped) && !player.lastInput.btnJump)
            {
                // Jump
                velocity.y = jumpSpeed * jumpFactor * 35f / GameManager.singleton.fracunitsPerM;

                if (!isResimmingMovement)
                    GameSounds.PlaySound(gameObject, jumpSound);
                state |= State.Jumped;
            }
            else if (!state.HasFlag(State.Thokked) && state.HasFlag(State.Jumped) && !player.lastInput.btnJump)
            {
                // Thok
                velocity.SetHorizontal(player.aimForward.Horizontal().normalized * (actionSpeed / GameManager.singleton.fracunitsPerM * 35f));

                if (!isResimmingMovement)
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
        state &= ~(State.Jumped | State.Thokked | State.CanceledJump | State.Pained);
        velocity = velocity - direction * (Vector3.Dot(direction, velocity) / Vector3.Dot(direction, direction)) + force * direction;
    }

    private void HandleServerMovement()
    {
        // Add to movement history while removing old stuff
        movementHistory.Insert(0, new TimePosition(this) { time = Netplay.singleton.localPlayerTime });
        for (int i = 0; i < movementHistory.Count; i++)
        {
            if (movementHistory[i].time < Netplay.singleton.localPlayerTime - maxMovementHistory)
            {
                movementHistory.RemoveRange(i, movementHistory.Count - i);
                break;
            }
        }

        if (Netplay.singleton.isServer)
        {
            // move the player to the client's remote position - if it's valid. todo: check and resimulate
            if (Vector3.Distance(player.remotePosition, transform.position) < 2)
            {
                transform.position = player.remotePosition;
            }
        }
        else if (!Netplay.singleton.isServer)
        {
            if (player.playerId == Netplay.singleton.localPlayerId && Netplay.singleton.lastProcessedServerTick != null)
            {
                Vector3 serverPosition = player.remotePosition;
                float serverLocalTime = Netplay.singleton.localPlayerTime - Netplay.singleton.GetPing();

                isResimmingMovement = true;

                try
                {
                    int tickHistoryIndex;
                    for (tickHistoryIndex = 0; tickHistoryIndex < movementHistory.Count; tickHistoryIndex++)
                    {
                        if (movementHistory[tickHistoryIndex].time == serverLocalTime)
                            break;
                    }

                    // Reconcile movement if it's not accurate
                    if (tickHistoryIndex < movementHistory.Count - 1 /* && movementHistory[tickHistoryIndex].position != serverPosition */)
                    {
                        // rewind to the server position and resimulate from there
                        movementHistory[tickHistoryIndex].To(this);
                        transform.position = serverPosition;

                        for (int i = tickHistoryIndex; i >= 1; i--)
                        {
                            movementHistory[tickHistoryIndex].position = transform.position;

                            player.lastInput = movementHistory[i + 1].inputs;
                            player.input = movementHistory[i].inputs;
                            FrameUpdate(movementHistory[i - 1].time - movementHistory[i].time);
                        }

                        // restore inputs
                        player.input = movementHistory[0].inputs;
                        player.lastInput = movementHistory[1].inputs;
                        movementHistory[0].position = transform.position;
                    }
                    else
                        Debug.Log("Problem");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Exception occurred during resim: {e}. This was caught to avoid locking the player");
                }

                isResimmingMovement = false;
            }
            else if (player.playerId != Netplay.singleton.localPlayerId)
            {
                // other players just be movin
                transform.position = player.remotePosition;
            }
        }
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

        if (originalPosition != transform.position || originalVelocity != velocity)
            Debug.Log($"Resync {player.playerId}@{World.live.time.ToString("0.0")}");
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