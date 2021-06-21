using System;
using UnityEngine;

[RequireComponent(typeof(Character))]
public class CharacterMovement : Movement
{
    public enum CollisionType
    {
        Penetration,
        Cast
    }

    public enum JumpAbility
    {
        Thok,
        Glide
    }

    private Character player;
    private PlayerSounds sounds;
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
    public float wallRunSpeedThreshold = 10f;
    public float wallRunRotationResetSpeed = 180f;
    [Tooltip("When wall running on the previous frame, this force pushes you down towards the ground on the next frame if in range and running fast enough. Based on how much your orientation diverged from up=(0,1,0) in degrees. 1 means push fully to the ground.")]
    public AnimationCurve wallRunPushForceByUprightness = AnimationCurve.Linear(0, 0, 180, 1);
    public float wallRunEscapeVelocity = 6f;
    public float wallRunTestDepth = 0.3f;
    public float wallRunTestRadius = 0.4f;
    public float wallRunTestHeight = 0.08f;
    [Tooltip("Tests the average normal of the three points against the normal of the sensor triangle. If the angle diverges above this amount, the up vector is reset. Used to avoid treating small steps as slopes.")]
    public float wallRunAngleFromAverageNormalMaxTolerance = 10.0f;
    public bool wallRunCameraAssist = true;
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
    public JumpAbility jumpAbility;
    public float actionSpeed = 60;

    public GlideSettings glide;

    [Header("Spindash and Roll")]
    public float minRollSpeed = 1f;
    [Range(0f, 1f)]
    public float rollingAccelerationMultiplier = 0.5f;
    [Range(0f, 1f)]
    public float rollingFriction = 0.999f;

    public float spindashChargeDuration = 1f;
    public float spindashMaxSpeed = 20f;
    public float spindashMinSpeed = 1f;

    [Header("Sounds")]
    public GameSound jumpSound = new GameSound();
    public GameSound thokSound = new GameSound();

    [Header("Debug")]
    public bool debugDisableCollision = false;
    public bool debugDrawMovement = false;
    public bool debugDrawWallrunSensors = false;

    // States
    [Flags]
    public enum State : byte
    {
        Jumped   = 1,
        Rolling  = 2,
        Thokked  = 4,
        CanceledJump = 8,
        Pained = 16,
        Gliding = 32
    };
    public State state { get; set; }

    // while charging a spindash, how much it's charged
    public float spindashChargeLevel;

    /// <summary>
    /// Retrieves the velocity vector along the current ground plane (horizontal if you're in the air)
    /// </summary>
    public Vector3 groundVelocity
    {
        get => velocity.AlongPlane(groundNormal);
        set => velocity.SetAlongPlane(groundNormal, value);
    }

    /// <summary>
    /// Returns the velocity vector along the plane of your character's up vector
    /// </summary>
    public Vector3 runVelocity
    {
        get => velocity.AlongPlane(up);
        set => velocity.SetAlongPlane(up, value);
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

    /// <summary>
    /// Current up vector
    /// </summary>
    public Vector3 up
    {
        get => _up;
        set
        {
            // change look rotation with wall run rotation motion if wallRunCameraAssist is enabled. Recompressed up to prevent drift when saving/loading quantized state
            if (wallRunCameraAssist && Netplay.singleton.localPlayer == player)
            {
                GameTicker.singleton.localPlayerInput.aimDirection = Quaternion.FromToRotation(CharacterState.RecompressUp(_up), CharacterState.RecompressUp(value)) * GameTicker.singleton.localPlayerInput.aimDirection;
            }

            _up = value;
        }
    }
    private Vector3 _up = Vector3.up;

    private Vector3 gravityDirection = new Vector3(0, -1, 0);

    // debugging movement frame step
    private bool doStep = false;

    private Vector3 debugPausePosition;
    private Quaternion debugPauseRotation;
    private Vector3 debugPauseVelocity;
    private Vector3 debugPauseUp;

    private RaycastHit[] bufferedHits = new RaycastHit[16];

    void Awake()
    {
        player = GetComponent<Character>();
        move = GetComponent<Movement>();
        sounds = GetComponent<PlayerSounds>();
    }

    public void TickMovement(float deltaTime, PlayerInput input, bool isRealtime = true)
    {
        Physics.SyncTransforms();

        if (isRealtime)
            DebugPauseStart();

        // Check whether on ground
        bool wasGroundDetected = DetectGround(Mathf.Max(wallRunTestDepth, groundTestDistance), out float _groundDistance, out Vector3 _groundNormal, out GameObject _groundObject);

        groundNormal = Vector3.up;
        groundDistance = float.MaxValue;

        isOnGround = wasGroundDetected && _groundDistance < groundTestDistance;

        if (wasGroundDetected)
        {
            if (enableWallRun)
                groundNormal = _groundNormal;
            else
                groundNormal = Vector3.up; // why are we doing this..? needed for steps to work. think it prevents player from wallrunning up steps or something... not working very well

            groundDistance = _groundDistance;
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

            state &= ~(State.Pained | State.Gliding);
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
        if (isOnGround && inputRunDirection.sqrMagnitude == 0 && groundVelocity.magnitude < stopSpeed / GameManager.singleton.fracunitsPerM * 35f)
            groundVelocity = Vector3.zero;

        // Jump button
        HandleJumpAbilities(input, deltaTime, isRealtime);

        // Spin button
        HandleSpinAbilities(input, deltaTime, isRealtime);

        // 3D rotation - do this after movement to encourage push down
        ApplyRotation(deltaTime, input);

        // Apply rideables
        ApplyRide(_groundObject);

        // Final movement
        ApplyFinalMovement(deltaTime, isRealtime);

        if (isRealtime)
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

    private bool DetectGround(float testDistance, out float foundDistance, out Vector3 groundNormal, out GameObject groundObject)
    {
        groundNormal = Vector3.up;
        foundDistance = -1f;
        groundObject = null;

        if (!debugDisableCollision)
        {
            int numHits = move.ColliderCast(bufferedHits, transform.position, -up, testDistance, landableCollisionLayers, QueryTriggerInteraction.Ignore, 0.1f);
            float closestGroundDistance = testDistance;

            for (int i = 0; i < numHits; i++)
            {
                if (bufferedHits[i].collider.GetComponentInParent<CharacterMovement>() != this && bufferedHits[i].distance <= closestGroundDistance + Mathf.Epsilon)
                {
                    groundNormal = bufferedHits[i].normal;
                    foundDistance = bufferedHits[i].distance;
                    groundObject = bufferedHits[i].collider.gameObject;
                    closestGroundDistance = foundDistance;
                }
            }

            return groundObject != null;
        }
        else // if (debugDisableCollision)
        {
            return transform.position.y <= 0;
        }
    }

    private void ApplyFriction(float deltaTime)
    {
        float currentFriction = friction;

        if ((state & State.Rolling) != 0)
            currentFriction = rollingFriction;

        if (groundVelocity.magnitude > 0 && isOnGround)
            groundVelocity = velocity * Mathf.Pow(currentFriction, deltaTime * 35f);
    }

    private void ApplyRunAcceleration(float deltaTime, PlayerInput input)
    {
        if ((state & (State.Pained | State.Gliding)) != 0)
            return; // cannot accelerate in these states

        Vector3 aim = input.aimDirection;
        Vector3 groundForward = aim.AlongPlane(groundNormal).normalized, groundRight = Vector3.Cross(up, aim).normalized;

        inputRunDirection = Vector3.ClampMagnitude(groundForward * input.moveVerticalAxis + groundRight * input.moveHorizontalAxis, 1);

        velocity *= GameManager.singleton.fracunitsPerM / 35f;
        float speed = groundVelocity.magnitude; // todo: use rmomentum
        float currentAcceleration = accelStart + speed * acceleration; // divide by scale in the real game

        if (!isOnGround)
            currentAcceleration *= airAccelerationMultiplier;
        if ((state & State.Rolling) != 0)
            currentAcceleration *= rollingAccelerationMultiplier;

        velocity += inputRunDirection * (50 * thrustFactor * currentAcceleration / 65536f * deltaTime * 35f);
        velocity /= GameManager.singleton.fracunitsPerM / 35f;
    }

    private void ApplyTopSpeedLimit(float lastHorizontalSpeed)
    {
        float speedToClamp = groundVelocity.magnitude;

        // speed limit doesn't apply while rolling
        if ((state & State.Rolling) == 0 && speedToClamp > topSpeed / GameManager.singleton.fracunitsPerM * 35f && speedToClamp > lastHorizontalSpeed)
            groundVelocity = (groundVelocity * (Mathf.Max(lastHorizontalSpeed, topSpeed / GameManager.singleton.fracunitsPerM * 35f) / speedToClamp));
    }

    public void ApplyHitKnockback(Vector3 force)
    {
        state |= State.Pained;
        velocity = force;
    }

    private void HandleJumpAbilities(PlayerInput input, float deltaTime, bool isRealtime)
    {
        if (state.HasFlag(State.Pained))
            return;

        if (input.btnJumpPressed)
        {
            // Start jump
            if (isOnGround && !state.HasFlag(State.Jumped))
            {
                velocity.SetAlongAxis(groundNormal, jumpSpeed * jumpFactor * 35f / GameManager.singleton.fracunitsPerM);

                if (isRealtime)
                    sounds.PlayNetworked(PlayerSounds.PlayerSoundType.Jump);

                state |= State.Jumped;
            }
            // Start jump abilities
            else if (state.HasFlag(State.Jumped) && !player.isHoldingFlag)
            {
                switch (jumpAbility)
                {
                    case JumpAbility.Thok:
                        if (!state.HasFlag(State.Thokked) && state.HasFlag(State.Jumped))
                        {
                            // Thok
                            velocity.SetHorizontal(input.aimDirection.Horizontal().normalized * (actionSpeed / GameManager.singleton.fracunitsPerM * 35f));

                            if (isRealtime)
                                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.Thok);

                            state |= State.Thokked;
                        }
                        break;
                    case JumpAbility.Glide:
                    {
                        state |= State.Gliding;

                        // give an initial boost towards facing direction
                        float clampedHorizontalMag = velocity.Horizontal().magnitude;
                        if (clampedHorizontalMag < glide.minSpeed)
                            velocity.SetHorizontal(input.aimDirection.Horizontal().normalized * glide.minSpeed);
                        break;
                    }
                }
            }
        }
        
        // Cancel jumps
        if (input.btnJumpReleased && state.HasFlag(State.Jumped) && !state.HasFlag(State.CanceledJump))
        {
            state |= State.CanceledJump;

            if (velocity.y > 0)
                velocity.y /= 2f;
        }

        // Run other abilities
        HandleJumpAbilities_Gliding(input, deltaTime);
    }

    private void HandleSpinAbilities(PlayerInput input, float deltaTime, bool isRealtime)
    {
        if (isOnGround && input.btnSpinPressed)
        {
            state |= State.Rolling;

            if (isRealtime)
                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.SpinCharge);
        }

        if ((state & State.Rolling) != 0 && input.btnSpin)
        {
            spindashChargeLevel = Mathf.Min(spindashChargeLevel + deltaTime / spindashChargeDuration, 1f);
        }
        else if ((state & State.Rolling) != 0 && input.btnSpinReleased)
        {
            groundVelocity = input.aimDirection.AlongPlane(groundNormal).normalized * (spindashMaxSpeed * spindashChargeLevel);
            spindashChargeLevel = 0f;

            if (isRealtime)
                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.SpinRelease);
        }
        else if ((state & State.Rolling) == 0f)
        {
            spindashChargeLevel = 0f;
        }

        if ((groundVelocity.magnitude < minRollSpeed && !input.btnSpin)
            || (state & State.Jumped) != 0)
        {
            state &= ~State.Rolling;
            spindashChargeLevel = 0f;
        }
    }

    private void HandleJumpAbilities_Gliding(PlayerInput input, float deltaTime)
    {
        Vector3 aim = input.aimDirection;

        // Cancel gliding upon release
        if (!input.btnJump)
        {
            if ((state & State.Gliding) != 0)
                state &= ~(State.Gliding | State.Jumped); // remove jumping state as well to prevent next glide
        }

        // Handle gliding
        if ((state & State.Gliding) == State.Gliding)
        {
            float horizontalSpeed = velocity.Horizontal().magnitude;
            Vector3 horizontalAim = aim.Horizontal().normalized;
            Vector3 desiredDirection = (horizontalAim + Vector3.Cross(Vector3.up, horizontalAim) * input.moveHorizontalAxis).normalized;

            // gravity cancel and fall control
            velocity.y = Math.Max(velocity.y, -glide.fallSpeedBySpeed.Evaluate(horizontalSpeed));

            // speed up/slow down
            float targetSpeed = horizontalSpeed + glide.accelerationBySpeed.Evaluate(horizontalSpeed) * input.moveVerticalAxis * deltaTime;

            velocity.SetHorizontal(velocity.Horizontal().normalized * targetSpeed);

            // turn!
            float turnSpeed = glide.turnSpeedBySpeed.Evaluate(horizontalSpeed); // in degrees/sec

            velocity.SetHorizontal(Vector3.RotateTowards(velocity.Horizontal(), desiredDirection, turnSpeed * Mathf.Deg2Rad * deltaTime, 0f));

            // speed clamps
            Vector3 clampedHorizontal = velocity.Horizontal();
            float clampedHorizontalMag = clampedHorizontal.magnitude;
            if (clampedHorizontalMag < glide.minSpeed)
                velocity.SetHorizontal(clampedHorizontal * (glide.minSpeed / clampedHorizontalMag));
            else if (clampedHorizontalMag > glide.maxSpeed)
                velocity.SetHorizontal(clampedHorizontal * (glide.maxSpeed / clampedHorizontalMag));
        }
    }

    System.Collections.Generic.List<int> triangleBuffer = new System.Collections.Generic.List<int>(9999);
    System.Collections.Generic.List<Vector3> normalBuffer = new System.Collections.Generic.List<Vector3>(9999);

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
        Vector3 central = position + up * wallRunTestHeight;
        Vector3 frontLeftHit = default, frontRightHit = default, backHit = default;
        Vector3 frontLeftNormal = default, frontRightNormal = default, backNormal = default;
        RaycastHit centralHitInfo = default;
        int numSuccessfulCollisions = 0;

        // Detect our target rotation using three sensors
        for (int i = 0; i < 4; i++)
        {
            Vector3 start = i == 0 ? frontLeft : (i == 1 ? frontRight : (i == 2 ? back : central));
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
                    case 3:
                        centralHitInfo = hit;
                        break;
                }
                
                numSuccessfulCollisions++;
                color = Color.green;
            }

            if (debugDrawWallrunSensors)
                Debug.DrawLine(start, start - up * wallRunTestDepth, color);
        }

        // If all sensors were activated, we can rotate
        if (numSuccessfulCollisions == 4)
        {
            // Smooth wall running, when the mesh allows it
            MeshCollider backMeshCollider = centralHitInfo.collider as MeshCollider;
            bool hasFoundSmoothNormal = false;
            if (backMeshCollider != null && backMeshCollider.sharedMesh.isReadable)
            {
                int index = centralHitInfo.triangleIndex * 3;
                Mesh mesh = backMeshCollider.sharedMesh;
                
                mesh.GetNormals(normalBuffer);

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    int start = mesh.GetSubMesh(i).indexStart;
                    int count = mesh.GetSubMesh(i).indexCount;

                    if (index >= start && index < start + count)
                    {
                        mesh.GetIndices(triangleBuffer, i);

                        Vector3 smoothNormal = normalBuffer[triangleBuffer[index - start]] * centralHitInfo.barycentricCoordinate.x +
                            normalBuffer[triangleBuffer[index - start + 1]] * centralHitInfo.barycentricCoordinate.y +
                            normalBuffer[triangleBuffer[index - start + 2]] * centralHitInfo.barycentricCoordinate.z;

                        targetUp = backMeshCollider.transform.TransformDirection(smoothNormal).normalized;
                        hasFoundSmoothNormal = true;
                        break;
                    }
                }
            }

            if (!hasFoundSmoothNormal)
            {
                // Set target up vector from the three points
                targetUp = Vector3.Cross(frontRightHit - frontLeftHit, backHit - frontLeftHit).normalized;

                // If the polygons comprising the angle diverge strongly from the angle we've determined, recognise it as a step and not a slope
                float varianceFromAverageNormal = Mathf.Acos(Vector3.Dot((frontLeftNormal + frontRightNormal + backNormal).normalized, targetUp)) * Mathf.Rad2Deg;

                if (varianceFromAverageNormal > wallRunAngleFromAverageNormalMaxTolerance)
                    targetUp = (frontLeftNormal + frontRightNormal + backNormal).normalized;
            }

            // Push down towards the ground
            if (deltaTime > 0f && verticalVelocity < wallRunEscapeVelocity)
            {
                float averageDistance = Vector3.Distance((frontLeftHit + frontRightHit + backHit) / 3, position);
                float forceMultiplier = wallRunPushForceByUprightness.Evaluate(Mathf.Acos(Vector3.Dot(targetUp, Vector3.up)) * Mathf.Rad2Deg);
                velocity += -targetUp * (averageDistance / deltaTime * forceMultiplier);
            }

            // this is our new ground normal
            groundNormal = targetUp;
        }

        if (velocity.magnitude < wallRunSpeedThreshold)
            targetUp = Vector3.up;

        // Rotate towards our target
        if (Vector3.Angle(up, targetUp) > 0f)
        {
            float degreesToRotate = wallRunRotationResetSpeed * deltaTime;

            up = Vector3.Slerp(up, targetUp, Mathf.Min(degreesToRotate / Vector3.Angle(up, targetUp), 1.0f)); // todo : might differ for frame rate, might be the reconciliation issue?
        }

        // Apply final rotation
        transform.rotation = Quaternion.LookRotation(input.aimDirection.AlongPlane(up), up);

        if ((state & State.Gliding) != 0)
            transform.rotation = Quaternion.LookRotation(velocity, up);
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

    private void ApplyFinalMovement(float deltaTime, bool isRealtime)
    {
        // Perform final movement and collision
        Vector3 originalPosition = transform.position;
        Vector3 originalVelocity = velocity;
        Vector3 stepUpVector = up * maxStepHeight;
        bool stepUp = maxStepHeight > 0f;

        move.enableCollision = !debugDisableCollision;

        if (stepUp)
            transform.position += stepUpVector;

        if (collisionType == CollisionType.Penetration)
            move.MovePenetration(velocity * deltaTime, isRealtime);
        else
            move.Move(velocity * deltaTime, out RaycastHit _, isRealtime);

        if (stepUp)
        {
            Vector3 stepReturn = -stepUpVector;
            bool doStepDownwards = false;

            if (isOnGround && velocity.AlongAxis(groundNormal) <= groundingForce + 0.001f)
            {
                stepReturn -= stepUpVector; // step _down_ as well
                doStepDownwards = true;
            }

            if (!move.Move(stepReturn, out RaycastHit _, isRealtime, MoveFlags.NoSlide) && doStepDownwards)
            {
                // we didn't hit a step on the way down? then don't step downwards
                transform.position += stepUpVector;
            }
        }

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

            // don't accumulate vertical velocity from stepping up, that's another fishy sign
            if (stepUp && velocity.AlongAxis(stepUpVector) > originalVelocity.AlongAxis(stepUpVector))
                velocity.SetAlongAxis(stepUpVector, originalVelocity.AlongAxis(stepUpVector));

            // overall, don't let velocity exceed its original magnitude, its a sign made of fish
            velocity = Vector3.ClampMagnitude(velocity, originalVelocity.magnitude);
        }

        if (debugDrawMovement)
        {
            DebugExtension.DebugCapsule(originalPosition, originalPosition + originalVelocity * deltaTime, Color.red, 0.1f);
            DebugExtension.DebugCapsule(originalPosition, originalPosition + velocity * deltaTime, Color.blue, 0.1f);
        }
    }

    void ApplyRide(GameObject target)
    {
        if (target != null && target.TryGetComponent(out Rideable rideable))
        {
            //rideable.GetFrameMovementDelta();
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