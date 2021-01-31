using System;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class CharacterMovement : Movement
{
    public enum CollisionType
    {
        Penetration,
        Cast
    }

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
    [Tooltip("When wall running on the previous frame, this force pushes you down towards the ground on the next frame if in range and running fast enough. Based on how much your orientation diverged from up=(0,1,0) in degrees. 1 means push fully to the ground.")]
    public AnimationCurve wallRunPushForceByUprightness = AnimationCurve.Linear(0, 0, 180, 1);
    public float wallRunEscapeVelocity = 6f;
    public float wallRunTestDepth = 0.3f;
    public float wallRunTestRadius = 0.4f;
    public float wallRunTestHeight = 0.08f;
    [Tooltip("Tests the average normal of the three points against the normal of the sensor triangle. If the angle diverges above this amount, the up vector is reset. Used to avoid treating small steps as slopes.")]
    public float wallRunAngleFromAverageNormalMaxTolerance = 10.0f;
    public Transform rotateableModel;

    [Header("Collision")]
    public CollisionType collisionType = CollisionType.Penetration;
    public float groundTestDistance = 0.05f;
    public float groundingForce = 3f;
    public float groundingEscapeVelocity = 1f;
    public LayerMask landableCollisionLayers;

    [Header("Step")]
    public float maxStepHeight = 0.4f;

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

    public float verticalVelocity
    {
        get => velocity.AlongAxis(up);
        set => velocity.SetAlongAxis(up, value);
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

    private Vector3 extraMoveOffset;

    /// <summary>
    /// Current up vector
    /// </summary>
    public Vector3 up { get; set; } = Vector3.up;

    private Vector3 gravityDirection = new Vector3(0, -1, 0);

    private bool showDebugLines => Application.platform == RuntimePlatform.WindowsEditor;

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
        this.extraMoveOffset = Vector3.zero;

        // Check whether on ground
        bool wasGroundDetected = DetectGround(Mathf.Max(wallRunTestDepth, groundTestDistance), out float _groundDistance, out Vector3 _groundNormal);

        isOnGround = wasGroundDetected && _groundDistance < groundTestDistance;

        if (wasGroundDetected)
        {
            if (!enableWallRun)
                groundNormal = _groundNormal;
            groundDistance = _groundDistance;
        }
        else
        {
            if (!enableWallRun)
                groundNormal = Vector3.up;
            groundDistance = float.MaxValue;
        }

        // Apply grounding effects straight away so we can be more up-to-date with wallrunning stuff
        ApplyGrounding();

        // Add/remove states depending on whether isOnGround
        if (isOnGround && velocity.AlongAxis(up) <= 0.5f)
        {
            if (state.HasFlag(State.Pained))
            {
                //player.damageable.StartInvincibilityTime();
            }

            state &= ~State.Pained;
        }

        // Friction
        ApplyFriction(deltaTime);
        float lastHorizontalSpeed = groundVelocity.magnitude; // this is our new max speed if our max speed was already exceeded

        // Run
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

        // 3D rotation - do this after movement to encourage push down
        ApplyRotation(deltaTime, input);

        // Final movement
        ApplyFinalMovement(deltaTime, isReconciliation);

        if (!isReconciliation)
            DebugPauseEnd();
    }

    public Vector3 axis;

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
            RaycastHit[] hits = new RaycastHit[10];
            int numHits = move.ColliderCast(hits, transform.position, -up, testDistance, landableCollisionLayers, QueryTriggerInteraction.Ignore, 0.1f);
            float closestGroundDistance = testDistance;
            bool isFound = false;

            for (int i = 0; i < numHits; i++)
            {
                if (hits[i].collider.GetComponentInParent<CharacterMovement>() != this && hits[i].distance <= closestGroundDistance + Mathf.Epsilon)
                {
                    isFound = true;
                    groundNormal = hits[i].normal;
                    foundDistance = hits[i].distance;
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
            else if (!state.HasFlag(State.Thokked) && state.HasFlag(State.Jumped) && !player.isHoldingFlag)
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

        Vector3 targetUp = Vector3.up;

        // We need to look ahead a bit so that we can push towards the ground after we've moved
        Vector3 position = transform.position + velocity * deltaTime;
        Vector3 frontRight = position + (transform.forward + transform.right) * wallRunTestRadius + up * wallRunTestHeight;
        Vector3 frontLeft = position + (transform.forward - transform.right) * wallRunTestRadius + up * wallRunTestHeight;
        Vector3 back = position - transform.forward * wallRunTestRadius + up * wallRunTestHeight;
        Vector3 frontLeftHit = default, frontRightHit = default, backHit = default;
        Vector3 frontLeftNormal = default, frontRightNormal = default, backNormal = default;
        int numSuccessfulCollisions = 0;

        // Detect our target rotation using three sensors
        for (int i = 0; i < 3; i++)
        {
            Vector3 start = i == 0 ? frontLeft : (i == 1 ? frontRight : back);
            Color color = Color.red;

            if (Physics.Raycast(start, -up, out RaycastHit hit, wallRunTestDepth, landableCollisionLayers, QueryTriggerInteraction.Ignore))
            {
                switch (i)
                {
                    case 0:
                        frontLeftHit = hit.point;
                        frontLeftNormal = hit.normal;
                        break;
                    case 1:
                        frontRightHit = hit.point;
                        frontRightNormal = hit.normal;
                        break;
                    case 2:
                        backHit = hit.point;
                        backNormal = hit.normal;
                        break;
                }
                
                numSuccessfulCollisions++;
                color = Color.green;
            }

            if (showDebugLines)
                Debug.DrawLine(start, start - up * wallRunTestDepth, color);
        }

        // If all sensors were activated, we can rotate
        if (numSuccessfulCollisions == 3)
        {
            // Set target up vector
            targetUp = Vector3.Cross(frontRightHit - frontLeftHit, backHit - frontLeftHit).normalized;

            // If the polygons comprising the angle diverge strongly from the angle we've determined, recognise it as a step and not a slope
            float varianceFromAverageNormal = Mathf.Acos(Vector3.Dot((frontLeftNormal + frontRightNormal + backNormal).normalized, targetUp)) * Mathf.Rad2Deg;

            if (varianceFromAverageNormal > wallRunAngleFromAverageNormalMaxTolerance)
                targetUp = (frontLeftNormal + frontRightNormal + backNormal).normalized;

            // Push down towards the ground
            if (deltaTime > 0f && verticalVelocity < wallRunEscapeVelocity)
            {
                float averageDistance = Vector3.Distance((frontLeftHit + frontRightHit + backHit) / numSuccessfulCollisions, position);
                float forceMultiplier = wallRunPushForceByUprightness.Evaluate(Mathf.Acos(Vector3.Dot(targetUp, Vector3.up)) * Mathf.Rad2Deg);
                velocity += -targetUp * (averageDistance / deltaTime * forceMultiplier);
            }

            // this is our new ground normal
            groundNormal = targetUp;
        }

        // Rotate towards our target
        if (Vector3.Angle(up, targetUp) > 0f)
        {
            float degreesToRotate = wallRunRotationResetSpeed * deltaTime;

            up = Vector3.Slerp(up, targetUp, Mathf.Min(degreesToRotate / Vector3.Angle(up, targetUp), 1.0f));
        }

        // Apply final rotation
        transform.rotation = Quaternion.LookRotation(input.aimDirection.AlongPlane(up), up);

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

    private void ApplyFinalMovement(float deltaTime, bool isReconciliation)
    {
        // Perform final movement and collision
        Vector3 originalPosition = transform.position;
        Vector3 originalVelocity = velocity;
        Vector3 stepVector = up * maxStepHeight;

        move.enableCollision = !debugDisableCollision;

        if (collisionType == CollisionType.Penetration)
        {
            transform.position += stepVector;
            originalPosition += stepVector;

            move.MovePenetration(velocity * deltaTime, isReconciliation);
        }
        else
            move.Move(velocity * deltaTime + extraMoveOffset, out RaycastHit _, isReconciliation);

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

        if (collisionType == CollisionType.Penetration)
        {
            move.MovePenetration(-stepVector, isReconciliation);
            originalPosition -= stepVector;
        }

        if (showDebugLines)
        {
            DebugExtension.DebugCapsule(originalPosition, originalPosition + originalVelocity * deltaTime, Color.red, 0.1f);
            DebugExtension.DebugCapsule(originalPosition, originalPosition + velocity * deltaTime, Color.blue, 0.1f);
        }
    }

    public void SpringUp(float force, Vector3 direction, bool doSpringAbsolutely)
    {
        state &= ~(State.Jumped | State.Thokked | State.CanceledJump | State.Pained);

        if (doSpringAbsolutely)
            velocity = direction * force;
        else
            velocity.SetAlongAxis(direction, force);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) // don't show with the other debug lines
        {
            Gizmos.color = Color.blue;
            Vector3 frontRight = transform.position + (transform.forward + transform.right) * wallRunTestRadius + transform.up * wallRunTestHeight;
            Vector3 frontLeft = transform.position + (transform.forward - transform.right) * wallRunTestRadius + transform.up * wallRunTestHeight;
            Vector3 back = transform.position - transform.forward * wallRunTestRadius + transform.up * wallRunTestHeight;

            Gizmos.DrawLine(frontRight, frontRight - transform.up * wallRunTestDepth);
            Gizmos.DrawLine(frontLeft, frontLeft - transform.up * wallRunTestDepth);
            Gizmos.DrawLine(back, back - transform.up * wallRunTestDepth);
        }
    }
}