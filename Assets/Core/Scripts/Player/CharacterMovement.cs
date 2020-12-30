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
        get; set;
    }

    /// <summary>
    /// Speed of the player right now
    /// </summary>
    //[HideInInspector] public Vector3 velocity;

    /// <summary>
    /// The direction the player is trying to run in
    /// </summary>
    private Vector3 inputRunDirection;

    /// <summary>
    /// Whether the player is on the ground
    /// </summary>
    [HideInInspector] public bool isOnGround = false;

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
        isOnGround = DetectOnGround();

        // Point towards relevent direction
        transform.rotation = Quaternion.Euler(0, input.horizontalAim, 0);

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

        ApplyRunAcceleration(deltaTime, input);

        // Gravity
        velocity += new Vector3(0, -1, 0) * (gravity * 35f * 35f / GameManager.singleton.fracunitsPerM * deltaTime);

        // Top speed clamp
        ApplyTopSpeedLimit(lastHorizontalSpeed);

        // Stop speed
        if (inputRunDirection.sqrMagnitude == 0 && velocity.Horizontal().magnitude < stopSpeed / GameManager.singleton.fracunitsPerM * 35f)
            velocity.SetHorizontal(Vector3.zero);

        // Jump button
        HandleJumpAbilities(input);

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
            move.Move(velocity * deltaTime, out hit, isReconciliation);
        }
    }

    public bool debugDisableCollision = true;

    private bool DetectOnGround()
    {
        if (debugDisableCollision)
            return transform.position.y <= 0;

        RaycastHit[] hits = new RaycastHit[10];
        int numHits = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.1f, -Vector3.up.normalized, hits, 0.199f, ~0, QueryTriggerInteraction.Ignore);

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

    private void ApplyRunAcceleration(float deltaTime, PlayerInput input)
    {
        if (state.HasFlag(State.Pained))
            return; // cannot accelerate while in pain

        inputRunDirection = transform.forward.Horizontal().normalized * input.moveVerticalAxis + transform.right.Horizontal().normalized * input.moveHorizontalAxis;

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

    private void HandleJumpAbilities(PlayerInput input)
    {
        if (state.HasFlag(State.Pained))
            return;

        if (input.btnJumpPressed)
        {
            if (isOnGround && !state.HasFlag(State.Jumped))
            {
                // Jump
                velocity.y = jumpSpeed * jumpFactor * 35f / GameManager.singleton.fracunitsPerM;

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

    public void SpringUp(float force, Vector3 direction)
    {
        state &= ~(State.Jumped | State.Thokked | State.CanceledJump | State.Pained);
        velocity = velocity - direction * (Vector3.Dot(direction, velocity) / Vector3.Dot(direction, direction)) + force * direction;
    }
}