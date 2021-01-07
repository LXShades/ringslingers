using System;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class CharacterMovement : Movement
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

    [Header("3D movement")]
    public bool enableWallRun = true;
    public float wallRunRotationResetSpeed = 180f;
    public Transform rotateableModel;

    [Header("Advanced")]
    public float groundTestDistance = 0.05f;
    public float wallRunTestDistance = 0.3f;

    [Header("Abilities")]
    public float actionSpeed = 60;

    [Header("Sounds")]
    public GameSound jumpSound = new GameSound();
    public GameSound thokSound = new GameSound();

    [Header("Debug")]
    public bool debugDisableCollision = true;

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
    public State state { get; set; }

    /// <summary>
    /// Retrieves the velocity vector along the current ground plane (horizontal if you're in the air)
    /// </summary>
    public Vector3 groundVelocity
    {
        get => velocity.AlongPlane(groundNormal);
        set => velocity.SetAlongPlane(groundNormal, value);
    }

    /// <summary>
    /// The direction the player is trying to run in
    /// </summary>
    private Vector3 inputRunDirection;

    /// <summary>
    /// Whether the player is on the ground
    /// </summary>
    public bool isOnGround { get; private set; }

    public Vector3 groundNormal { get; private set; }

    public float groundDistance { get; private set; }

    /// <summary>
    /// Current up vector
    /// </summary>
    public Vector3 up { get; set; } = Vector3.up;

    private Vector3 gravityDirection = new Vector3(0, -1, 0);

    // whether we're currently reconciling movement
    private bool isReconciling = false;

    void Awake()
    {
        player = GetComponent<Player>();
        move = GetComponent<Movement>();
    }

    public void TickMovement(float deltaTime, PlayerInput input, bool isReconciliation = false)
    {
        isReconciling = isReconciliation;

        // Check whether on ground
        bool wasFound = DetectGround(1f, out float _groundDistance, out Vector3 _groundNormal);

        isOnGround = wasFound && _groundDistance < groundTestDistance;

        if (wasFound)
        {
            groundNormal = _groundNormal;
            groundDistance = _groundDistance;
        }
        else
        {
            groundNormal = Vector3.up;
            groundDistance = float.MaxValue;
        }

        // Point towards relevent direction
        transform.rotation = Quaternion.Euler(0, input.horizontalAim, 0);

        // 3D movement
        ApplyWallRunRotation(deltaTime);

        // Add/remove states depending on whether isOnGround
        if (isOnGround)
        {
            if (state.HasFlag(State.Pained))
                player.StartInvincibilityTime();

            state &= ~State.Pained;
        }

        // Friction
        ApplyFriction(deltaTime);
        float lastHorizontalSpeed = groundVelocity.magnitude; // this is our new max speed if our max speed was already exceeded

        ApplyRunAcceleration(deltaTime, input);

        // Gravity
        ApplyGravity(deltaTime);

        // Top speed clamp
        ApplyTopSpeedLimit(lastHorizontalSpeed);

        // Stop speed
        if (inputRunDirection.sqrMagnitude == 0 && groundVelocity.magnitude < stopSpeed / GameManager.singleton.fracunitsPerM * 35f)
            groundVelocity = Vector3.zero;

        // Jump button
        HandleJumpAbilities(input);

        // Do not slip through the ground
        ApplyGrounding();

        // Perform final movement and collision
        move.enableCollision = !debugDisableCollision;
        move.Move(velocity * deltaTime, out RaycastHit _, isReconciliation);
    }

    private bool DetectGround(float testDistance, out float foundDistance, out Vector3 groundNormal)
    {
        groundNormal = Vector3.up;
        foundDistance = -1f;

        if (!debugDisableCollision)
        {
            const float kUpTestDistance = 0.05f; // buffer in case slightly slipping through, awkward physics prevention etc
            RaycastHit[] hits = new RaycastHit[10];
            int numHits = move.ColliderCast(hits, transform.position + up * kUpTestDistance, -up.normalized, testDistance + kUpTestDistance, blockingCollisionLayers, QueryTriggerInteraction.Ignore);
            float closestGroundDistance = kUpTestDistance + testDistance;
            bool isFound = false;

            for (int i = 0; i < numHits; i++)
            {
                if (hits[i].collider.GetComponentInParent<CharacterMovement>() != this && hits[i].distance - kUpTestDistance < closestGroundDistance)
                {
                    isFound = true;
                    groundNormal = hits[i].normal;
                    foundDistance = hits[i].distance - kUpTestDistance;
                    closestGroundDistance = foundDistance;
                }
            }

            return isFound;
        }
        else // if (debugDisableCollision)
        {
            return transform.position.y <= 0;
        }
    }

    private void ApplyFriction(float deltaTime)
    {
        // Friction
        if (groundVelocity.magnitude > 0 && isOnGround)
            groundVelocity = velocity * Mathf.Pow(friction, deltaTime * 35f);
    }

    private void ApplyRunAcceleration(float deltaTime, PlayerInput input)
    {
        if (state.HasFlag(State.Pained))
            return; // cannot accelerate while in pain

        Vector3 aim = input.aimDirection;
        Vector3 groundForward = aim.AlongPlane(groundNormal).normalized, groundRight = Vector3.Cross(up, aim).normalized;

        inputRunDirection = Vector3.ClampMagnitude(groundForward * input.moveVerticalAxis + groundRight * input.moveHorizontalAxis, 1);

        velocity *= GameManager.singleton.fracunitsPerM / 35f;
        float speed = groundVelocity.magnitude; // todo: use rmomentum
        float currentAcceleration = accelStart + speed * acceleration; // divide by scale in the real game

        if (!isOnGround)
            currentAcceleration *= airAccelerationMultiplier;

        velocity += inputRunDirection * (50 * thrustFactor * currentAcceleration / 65536f * deltaTime * 35f);
        velocity /= GameManager.singleton.fracunitsPerM / 35f;
    }

    private void ApplyTopSpeedLimit(float lastHorizontalSpeed)
    {
        float speedToClamp = groundVelocity.magnitude;

        if (speedToClamp > topSpeed / GameManager.singleton.fracunitsPerM * 35f && speedToClamp > lastHorizontalSpeed)
            groundVelocity = (groundVelocity * (Mathf.Max(lastHorizontalSpeed, topSpeed / GameManager.singleton.fracunitsPerM * 35f) / speedToClamp));
    }

    public void ApplyHitKnockback(Vector3 force)
    {
        state |= State.Pained;
        velocity = force;
    }

    private void HandleJumpAbilities(PlayerInput input)
    {
        if (state.HasFlag(State.Pained))
            return;

        if (input.btnJumpPressed)
        {
            if (isOnGround && !state.HasFlag(State.Jumped))
            {
                // Jump
                velocity.SetAlongAxis(groundNormal, jumpSpeed * jumpFactor * 35f / GameManager.singleton.fracunitsPerM);

                if (!isReconciling)
                    GameSounds.PlaySound(gameObject, jumpSound);
                state |= State.Jumped;
            }
            else if (!state.HasFlag(State.Thokked) && state.HasFlag(State.Jumped))
            {
                // Thok
                velocity.SetHorizontal(input.aimDirection.Horizontal().normalized * (actionSpeed / GameManager.singleton.fracunitsPerM * 35f));

                if (!isReconciling)
                    GameSounds.PlaySound(gameObject, thokSound);
                state |= State.Thokked;
            }
        }
        
        if (input.btnJumpReleased && state.HasFlag(State.Jumped) && !state.HasFlag(State.CanceledJump))
        {
            // Cancel jump
            state |= State.CanceledJump;

            if (velocity.y > 0)
                velocity.y /= 2f;
        }
    }

    private void ApplyWallRunRotation(float deltaTime)
    {
        if (!enableWallRun)
        {
            up = Vector3.up;
            return;
        }

        // Rotate towards our target
        Vector3 targetUp;
        Vector3 lastUp = up;

        if (groundDistance <= wallRunTestDistance)
        {
            targetUp = groundNormal;
            isOnGround = true;
        }
        else
            targetUp = Vector3.up;

        if (Vector3.Angle(up, targetUp) > 0f)
        {
            float degreesToRotate = wallRunRotationResetSpeed * deltaTime;
            up = Vector3.Slerp(up, targetUp, Mathf.Min(degreesToRotate / Vector3.Angle(up, targetUp), 1.0f));
        }

        // Apply final rotation
        transform.rotation = Quaternion.LookRotation(player.input.aimDirection.AlongPlane(groundNormal), up);

        //player.input.aimDirection = Quaternion.FromToRotation(lastUp, up) * player.input.aimDirection;// test?
    }

    private void ApplyGravity(float deltaTime)
    {
        if (!isOnGround) // wall run, etc cancels out gravity
            velocity += gravityDirection * (gravity * 35f * 35f / GameManager.singleton.fracunitsPerM * deltaTime);
    }

    private void ApplyGrounding()
    {
        if (isOnGround)
        {
            // Push towrads the ground, if we're not actively moving away from it
            if (velocity.AlongAxis(groundNormal) <= 1f)
            {
                velocity.SetAlongAxis(groundNormal, -groundDistance * 10f);
                state &= ~(State.Jumped | State.Thokked | State.CanceledJump);
            }
        }
    }

    public void SpringUp(float force, Vector3 direction)
    {
        state &= ~(State.Jumped | State.Thokked | State.CanceledJump | State.Pained);
        velocity = velocity - direction * (Vector3.Dot(direction, velocity) / Vector3.Dot(direction, direction)) + force * direction;
    }
}