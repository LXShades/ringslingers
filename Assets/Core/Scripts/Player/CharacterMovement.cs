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
    [Tooltip("When wall running on the previous frame, this force pushes you down towards the ground on the next frame if in range and running fast enough. This value is multiplied by your velocity")]
    public float wallRunPushForce = 1f;
    public float wallRunTestDepth = 0.3f;
    public float wallRunTestRadius = 0.4f;
    public float wallRunTestHeight = 0.08f;
    public Transform rotateableModel;

    [Header("Collision")]
    public float groundTestDistance = 0.05f;
    public float groundingForce = 3f;
    public float groundingEscapeVelocity = 1f;
    public LayerMask landableCollisionLayers;

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

    public Vector3 wallRunNormal { get; private set; }

    /// <summary>
    /// Current up vector
    /// </summary>
    public Vector3 up { get; set; } = Vector3.up;

    private Vector3 gravityDirection = new Vector3(0, -1, 0);

    // whether we're currently reconciling movement
    private bool isReconciling = false;

    // debugging collision step
    private bool doStep = false;

    private Vector3 debugPausePosition;
    private Quaternion debugPauseRotation;
    private Vector3 debugPauseVelocity;
    private Vector3 debugPauseUp;

    void Awake()
    {
        player = GetComponent<Player>();
        move = GetComponent<Movement>();
    }

    public void TickMovement(float deltaTime, PlayerInput input, bool isReconciliation = false)
    {
        if (!isReconciliation)
            DebugPauseStart();

        this.isReconciling = isReconciliation;

        // If we were wall running on the last frame, and below escape velocity, push downwards
        //ApplyWallRunPushForce(input);

        // Check whether on ground
        bool wasGroundDetected = DetectGround(Mathf.Max(wallRunTestDepth, groundTestDistance), out float _groundDistance, out Vector3 _groundNormal);

        isOnGround = wasGroundDetected && _groundDistance < groundTestDistance;

        if (wasGroundDetected)
        {
            groundNormal = _groundNormal;
            groundDistance = _groundDistance;
        }
        else
        {
            groundNormal = Vector3.up;
            groundDistance = float.MaxValue;
        }

        // 3D movement
        ApplyRotation(deltaTime, input);

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
        Vector3 originalPosition = transform.position;
        Vector3 originalVelocity = velocity;

        move.enableCollision = !debugDisableCollision;
        move.Move(velocity * deltaTime, out RaycastHit _, isReconciliation);

        DebugExtension.DebugCapsule(transform.position, transform.position + velocity * deltaTime, Color.red, 0.1f);

        if (deltaTime > 0 && velocity == originalVelocity) // something might have changed our velocity during this tick - for example, a spring. only recalculate velocity if that didn't happen
        {
            // recalculate velocity. but some collisions will force us out of the ground, etc..
            // normally we'd do this...
            //  -> velocity = (transform.position - originalPosition) / deltaTime;
            // but instead, let's restrict the pushback to the opposite of the vector and no further
            // in other words the dot product of velocityNormal and pushAway should be <= 0 >= -1
            Vector3 offset = originalVelocity * deltaTime;
            Vector3 pushAwayVector = transform.position - (originalPosition + offset);

            velocity += pushAwayVector / deltaTime;

            // don't let velocity invert, that's a fishy sign
            if (Vector3.Dot(velocity, originalVelocity) < 0f)
                velocity -= originalVelocity.normalized * Vector3.Dot(velocity, originalVelocity.normalized);

            // don't let velocity exceed its original magnitude, that's another fishy sign
            velocity = Vector3.ClampMagnitude(velocity, originalVelocity.magnitude);
        }

        DebugExtension.DebugCapsule(transform.position, transform.position + velocity * deltaTime, Color.blue, 0.1f);

        if (!isReconciliation)
            DebugPauseEnd();
    }

    private void DebugPauseStart()
    {
        if (Application.isEditor && UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
            doStep = !doStep;

        debugPauseUp = up;
        debugPausePosition = transform.position;
        debugPauseRotation = transform.rotation;
        debugPauseVelocity = velocity;
    }

    private void DebugPauseEnd()
    {
        if (doStep && !UnityEngine.InputSystem.Keyboard.current.nKey.wasPressedThisFrame)
        {
            transform.position = debugPausePosition;
            transform.rotation = debugPauseRotation;
            velocity = debugPauseVelocity;
            up = debugPauseUp;
        }
    }

    private bool DetectGround(float testDistance, out float foundDistance, out Vector3 groundNormal)
    {
        groundNormal = Vector3.up;
        foundDistance = -1f;

        if (!debugDisableCollision)
        {
            const float kUpTestDistance = 0.05f; // buffer in case slightly slipping through, awkward physics prevention etc
            RaycastHit[] hits = new RaycastHit[10];
            int numHits = move.ColliderCast(hits, transform.position + up * kUpTestDistance, -up.normalized, testDistance + kUpTestDistance, landableCollisionLayers, QueryTriggerInteraction.Ignore);
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

    private void ApplyRotation(float deltaTime, PlayerInput input)
    {
        if (!enableWallRun)
        {
            up = Vector3.up;
            transform.rotation = Quaternion.Euler(0, input.horizontalAim, 0);
            return;
        }

        Vector3 targetUp;
        Vector3 frontRight = transform.position + (transform.forward + transform.right) * wallRunTestRadius + up * wallRunTestHeight;
        Vector3 frontLeft = transform.position + (transform.forward - transform.right) * wallRunTestRadius + up * wallRunTestHeight;
        Vector3 back = transform.position - transform.forward * wallRunTestRadius + up * wallRunTestHeight;
        Vector3 frontLeftHit = default, frontRightHit = default, backHit = default;
        int numSuccessfulCollisions = 0;

        // Detect our target rotation
        for (int i = 0; i < 3; i++)
        {
            Vector3 start = i == 0 ? frontLeft : (i == 1 ? frontRight : back);
            Color color = Color.red;

            if (Physics.Raycast(start, -up, out RaycastHit hit, wallRunTestDepth, landableCollisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (i == 0) frontLeftHit = hit.point;
                if (i == 1) frontRightHit = hit.point;
                if (i == 2) backHit = hit.point;
                numSuccessfulCollisions++;
                color = Color.green;
            }

            Debug.DrawLine(start, start - up * wallRunTestDepth, color);
        }

        if (numSuccessfulCollisions == 3)
        {
            targetUp = Vector3.Cross(frontRightHit - frontLeftHit, backHit - frontLeftHit).normalized;

            if (isOnGround)
                velocity += -targetUp * (wallRunPushForce * deltaTime);
        }
        else
            targetUp = Vector3.up;

        // Rotate towards our target
        if (Vector3.Angle(up, targetUp) > 0f)
        {
            float degreesToRotate = wallRunRotationResetSpeed * deltaTime;

            up = Vector3.Slerp(up, targetUp, Mathf.Min(degreesToRotate / Vector3.Angle(up, targetUp), 1.0f));
        }

        // Apply final rotation
        transform.rotation = Quaternion.LookRotation(input.aimDirection.AlongPlane(groundNormal), up);

        // change look rotation with rotation?
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
            // Push towards the ground, if we're not actively moving away from it
            if (velocity.AlongAxis(groundNormal) <= groundingEscapeVelocity)
            {
                velocity.SetAlongAxis(groundNormal, -groundingForce);
                state &= ~(State.Jumped | State.Thokked | State.CanceledJump);
            }
        }
    }

    public void SpringUp(float force, Vector3 direction)
    {
        state &= ~(State.Jumped | State.Thokked | State.CanceledJump | State.Pained);
        velocity = velocity - direction * (Vector3.Dot(direction, velocity) / Vector3.Dot(direction, direction)) + force * direction;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Vector3 frontRight = transform.position + (transform.forward + transform.right) * wallRunTestRadius + transform.up * wallRunTestHeight;
        Vector3 frontLeft = transform.position + (transform.forward - transform.right) * wallRunTestRadius + transform.up * wallRunTestHeight;
        Vector3 back = transform.position - transform.forward * wallRunTestRadius + transform.up * wallRunTestHeight;

        Gizmos.DrawLine(frontRight, frontRight - transform.up * wallRunTestDepth);
        Gizmos.DrawLine(frontLeft, frontLeft - transform.up * wallRunTestDepth);
        Gizmos.DrawLine(back, back- transform.up * wallRunTestDepth);
    }
}