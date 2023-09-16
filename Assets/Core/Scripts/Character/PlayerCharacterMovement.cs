using System;
using UnityEngine;

// Character state is split into multiple sections where each state is exclusive
// Originally we had a single set of flags, but many states (actually I guess all of them lol) were mutually exclusive so it wasn't the most efficient way of storing and transmitting them
public enum CharacterMovementState : byte // 3 bits
{
    None,
    Jumped,
    CanceledJump,
    Thokked,
    Pained,
    Rolling,
    SpinCharging,
    Gliding,
    Climbing,
    Flying,
}

public class PlayerCharacterMovement : CharacterMovement
{
    const float kFracunitSpeedToMetreSpeed = 35f / 64f;
    const float kFracunitLengthToMetreLength = 1f / 64f;

    public enum JumpAbility
    {
        Thok,
        Glide
    }

    private Character player;
    private PlayerSounds sounds;

    [Header("[PlayerCharacterMovement] Stats")]
    public float accelStart = 96;
    public float acceleration = 40;
    public float thrustFactor = 5;
    public float topSpeed = 36 * kFracunitSpeedToMetreSpeed;

    public AnimationCurve accelCurve = new AnimationCurve();
    public AnimationCurve inverseAccelCurve = new AnimationCurve();

    public float friction = 0.90625f;
    public float stopSpeed = (1f * kFracunitLengthToMetreLength);

    public float jumpSpeed = (39f / 4f) * kFracunitSpeedToMetreSpeed;
    public float jumpFactor = 1;

    public float airAccelerationMultiplier = 0.25f;

    [Header("[PlayerCharacterMovement] Loopy settings")]
    public float loopySpeedRequirement = 10f;

    [Header("[PlayerCharacterMovement] Abilities")]
    public JumpAbility jumpAbility;
    public float actionSpeed = 60 * kFracunitSpeedToMetreSpeed;

    public GlideSettings glide;

    [Header("[PlayerCharacterMovement] Spindash and Roll")]
    public float minRollSpeed = 1f;
    [Range(0f, 1f)]
    public float rollingAccelerationMultiplier = 0.5f;
    [Range(0f, 1f)]
    public float rollingFriction = 0.999f;
    public float rollingCapsuleHeight = 0.5f;

    public float spindashChargeDuration = 1f;
    public float spindashMaxSpeed = 20f;
    public float spindashMinSpeed = 1f;
    [Range(0f, 1f)]
    public float spindashChargeFriction = 0.995f;

    [Header("[PlayerCharacterMovement] Gravity manipulation")]
    [Tooltip("When in spherical gravity environments, lateral movement can keep you in orbit due to affecting your altitude. Correction works by eliminating altitude change when gravity changes")]
    [Range(0f, 1f)]
    public float gravityAltitudeCorrectionFactor = 1f;

    [Header("[PlayerCharacterMovement] Debug")]
    public bool debugDrawMovement = false;

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
    /// Main character state, ability, etc
    /// </summary>
    public CharacterMovementState baseState { get; set; }

    /// <summary>
    /// Whether the character is spinning either by jumping or rolling, i.e. dangerous to touch
    /// </summary>
    public bool isSpinblading => baseState == CharacterMovementState.Rolling || baseState == CharacterMovementState.SpinCharging || isSpinbladingFromJump;

    /// <summary>
    /// Whether the character is jumping and spinning
    /// </summary>
    public bool isSpinbladingFromJump => baseState == CharacterMovementState.Jumped || baseState == CharacterMovementState.CanceledJump || baseState == CharacterMovementState.Thokked;

    /// <summary>
    /// The direction the player is trying to run in
    /// </summary>
    private Vector3 inputRunDirection;

    /// <summary>
    /// Whether the player is on the ground
    /// </summary>
    public bool isOnGround { get; private set; }

    public Vector3 groundNormal { get; private set; }

    // to restore collision capsule when rolling
    private float originalCapsuleHeight;

    protected virtual void Awake()
    {
        player = GetComponent<Character>();
        sounds = GetComponent<PlayerSounds>();

        originalCapsuleHeight = (colliders[0] as CapsuleCollider).height;
    }

    public void TickMovement(float deltaTime, CharacterInput input) => TickMovement(deltaTime, input, TickInfo.Default);

    public void TickMovement(float deltaTime, CharacterInput input, TickInfo tickInfo)
    {
        if (enableCollision)
            Physics.SyncTransforms();

        // Apply gravity volumes first
        gravityDirection = Vector3.down;
        GravityVolume.GetInfluences(transform.position, ref gravityDirection);

        // Check whether on ground
        CalculateGroundInfo(out GroundInfo groundInfo);

        isOnGround = groundInfo.isOnGround;
        groundNormal = groundInfo.normal;

        // Look direction
        if (baseState != CharacterMovementState.Climbing)
            forward = input.aimDirection;

        // Apply grounding effects straight away so we can be more up-to-date with wallrunning stuff
        ApplyGroundStates();

        // Add/remove states depending on whether isOnGround
        if (isOnGround && velocity.AlongAxis(up) <= 0.5f)
            RemoveStates(CharacterMovementState.Pained, CharacterMovementState.Gliding, CharacterMovementState.Climbing);

        // Friction
        ApplyFriction(deltaTime);
        float lastHorizontalSpeed = groundVelocity.magnitude; // this is our new max speed if our max speed was already exceeded

        // Run
        ApplyRunAcceleration(deltaTime, input);

        // Gravity
        if (baseState != CharacterMovementState.Climbing)
            ApplyCharacterGravity(groundInfo, deltaTime);

        // Top speed clamp
        ApplyTopSpeedLimit(lastHorizontalSpeed);

        // Stop speed
        if (isOnGround && inputRunDirection.sqrMagnitude == 0 && groundVelocity.magnitude < stopSpeed)
            groundVelocity = Vector3.zero;

        // Jump button
        HandleJumpAbilities(input, deltaTime, tickInfo);

        // Spin button
        HandleSpinAbilities(input, deltaTime, tickInfo);

        // 3D rotation - do this after movement to encourage push down
        //ApplyRotation(deltaTime, input);
        // enable/disable loopy movement based on our speed
        enableLoopy = velocity.AlongPlane(up).magnitude > loopySpeedRequirement;

        // Adjust character hitbox when spindashing/rolling
        float capsuleHeight = originalCapsuleHeight;
        CapsuleCollider capsule = colliders[0] as CapsuleCollider;
        if (isSpinblading && !isSpinbladingFromJump) // todo: shouldn't jumping also reduce hitbox size
            capsuleHeight = rollingCapsuleHeight;

        if (capsule.height != capsuleHeight)
        {
            capsule.height = capsuleHeight;
            capsule.center = new Vector3(capsule.center.x, Mathf.Max(capsuleHeight * 0.5f, capsule.radius), capsule.center.z);
        }

        // Altitude correction
        if (velocity.sqrMagnitude > 0.001f)
        {
            Vector3 nextGravity = Vector3.zero;

            if (GravityVolume.GetInfluences(transform.position + velocity * deltaTime, ref nextGravity))
            {
                float oldVerticalSpeed = velocity.AlongAxis(gravityDirection);
                float oldVelocityMagnitude = velocity.magnitude;
                Vector3 newVelocity = velocity;

                newVelocity.SetAlongAxis(nextGravity, oldVerticalSpeed);
                newVelocity *= oldVelocityMagnitude / newVelocity.magnitude;
                velocity = Vector3.Slerp(velocity, newVelocity, gravityAltitudeCorrectionFactor);
            }
        }

        // Final movement
        bool oldEnableStepUp = enableStepUp;
        if (baseState == CharacterMovementState.Climbing)
            enableStepUp = false;

        ApplyCharacterVelocity(groundInfo, deltaTime, tickInfo);

        enableStepUp = oldEnableStepUp;

        // Set final rotation
        Vector3 oldTransformUp = transform.up;
        transform.rotation = Quaternion.LookRotation(forward.AlongPlane(up), up);
        transform.position = transform.position + oldTransformUp * 0.5f - up * 0.5f;
    }

    private void ApplyFriction(float deltaTime)
    {
        float currentFriction = friction;

        if (baseState == CharacterMovementState.Rolling)
            currentFriction = rollingFriction;
        else if (baseState == CharacterMovementState.SpinCharging)
            currentFriction = spindashChargeFriction;

        if (groundVelocity.magnitude > 0 && isOnGround)
            groundVelocity = velocity * Mathf.Pow(currentFriction, deltaTime * 35f);
    }

    private void ApplyRunAcceleration(float deltaTime, CharacterInput input)
    {
        if (IsAnyState(CharacterMovementState.Pained, CharacterMovementState.Gliding, CharacterMovementState.Climbing, CharacterMovementState.SpinCharging))
            return; // cannot accelerate in these states

        Vector3 aim = input.aimDirection;
        Vector3 groundForward = aim.AlongPlane(groundNormal).normalized, groundRight = Vector3.Cross(up, aim).normalized;

        inputRunDirection = Vector3.ClampMagnitude(groundForward * input.moveVerticalAxis + groundRight * input.moveHorizontalAxis, 1);

        float speed = groundVelocity.magnitude; // todo: use rmomentum
        float currentAcceleration = Mathf.Max(accelCurve.Evaluate(inverseAccelCurve.Evaluate(speed) + deltaTime) - speed, 0f);

        if (!isOnGround)
            currentAcceleration *= airAccelerationMultiplier;
        if (baseState == CharacterMovementState.Rolling)
            currentAcceleration *= rollingAccelerationMultiplier;

        velocity += inputRunDirection * currentAcceleration;
    }

    private void ApplyTopSpeedLimit(float lastHorizontalSpeed)
    {
        float speedToClamp = groundVelocity.magnitude;

        // speed limit doesn't apply while rolling
        if (baseState != CharacterMovementState.Rolling && speedToClamp > topSpeed && speedToClamp > lastHorizontalSpeed)
            groundVelocity = (groundVelocity * (Mathf.Max(lastHorizontalSpeed, topSpeed) / speedToClamp));
    }

    public void ApplyHitKnockback(Vector3 force)
    {
        baseState = CharacterMovementState.Pained;
        velocity = force;
    }

    private void HandleJumpAbilities(CharacterInput input, float deltaTime, TickInfo tickInfo)
    {
        if (IsAnyState(CharacterMovementState.Pained))
            return;

        if (input.btnJumpPressed)
        {
            // Start regular jump
            if (baseState != CharacterMovementState.Jumped && (isOnGround || baseState == CharacterMovementState.Climbing))
            {
                if (baseState == CharacterMovementState.Climbing)
                {
                    // Climb jump
                    velocity.SetAlongAxis(forward.AlongPlane(gravityDirection), -jumpSpeed);
                    velocity.SetAlongAxis(up, jumpSpeed);
                }
                else
                {
                    // Regular ground jump
                    velocity.SetAlongAxis(groundNormal, jumpSpeed * jumpFactor);
                }

                if (tickInfo.isFullForwardTick)
                    sounds.PlayNetworked(PlayerSounds.PlayerSoundType.Jump);

                baseState = CharacterMovementState.Jumped;
            }
            // Start jump abilities
            else if (player == null || !player.isHoldingFlag)
            {
                switch (jumpAbility)
                {
                    case JumpAbility.Thok:
                        if (baseState == CharacterMovementState.CanceledJump)
                        {
                            // Thok
                            velocity.SetAlongPlane(gravityDirection, input.aimDirection.AlongPlane(gravityDirection).normalized * (actionSpeed / GameManager.singleton.fracunitsPerM * 35f));

                            if (tickInfo.isFullForwardTick)
                                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.Thok);

                            baseState = CharacterMovementState.Thokked;
                        }
                        break;
                    case JumpAbility.Glide:
                        if (isSpinbladingFromJump)
                        {
                            baseState = CharacterMovementState.Gliding;

                            // give an initial boost towards facing direction
                            float clampedHorizontalMag = velocity.Horizontal().magnitude;
                            if (clampedHorizontalMag < glide.startSpeed)
                                velocity.SetHorizontal(input.aimDirection.Horizontal().normalized * glide.startSpeed);
                        }
                        break;
                }
            }
        }
        
        // Cancel jumps
        if (input.btnJumpReleased && baseState == CharacterMovementState.Jumped)
        {
            baseState = CharacterMovementState.CanceledJump;

            float verticalSpeed = -velocity.AlongAxis(gravityDirection);
            if (verticalSpeed > 0f)
                velocity.SetAlongAxis(gravityDirection, -(verticalSpeed / 2f));
        }

        // Run other abilities
        HandleJumpAbilities_Gliding(input, deltaTime);
    }

    private void HandleSpinAbilities(CharacterInput input, float deltaTime, TickInfo tickInfo)
    {
        if (baseState == CharacterMovementState.Climbing)
            return;

        bool isMovingFastEnoughToRoll = groundVelocity.magnitude >= minRollSpeed;
        if (isMovingFastEnoughToRoll && baseState != CharacterMovementState.Rolling && baseState != CharacterMovementState.Jumped && isOnGround && input.btnSpinPressed) // we check State.Jumped as well because we may have jumped in this frame
        {
            baseState = CharacterMovementState.Rolling;

            if (tickInfo.isFullForwardTick)
                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.SpinRoll);
        }
        else if ((baseState == CharacterMovementState.Rolling || (isOnGround && !isMovingFastEnoughToRoll)) && input.btnSpinPressed)
        {
            spindashChargeLevel = 0f;
            baseState = CharacterMovementState.SpinCharging;
        }
        else if (baseState == CharacterMovementState.SpinCharging && input.btnSpin)
        {
            spindashChargeLevel = Mathf.Min(spindashChargeLevel + deltaTime / spindashChargeDuration, 1f);

            if (TimeTool.IsTick(tickInfo.time, deltaTime, 8) && tickInfo.isFullForwardTick)
                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.SpinCharge);
        }
        else if (baseState == CharacterMovementState.SpinCharging && input.btnSpinReleased)
        {
            Vector3 nextDirection = input.aimDirection.AlongPlane(groundNormal).normalized;
            float releaseSpeed = (spindashMaxSpeed * spindashChargeLevel);
            float factor = groundVelocity.magnitude > 0 ? Mathf.Clamp(releaseSpeed / Mathf.Max(groundVelocity.magnitude, 0.001f), 0f, 1f) : 1f;

            groundVelocity = Vector3.Lerp(groundVelocity, nextDirection * releaseSpeed, factor);

            if (factor > 0 && tickInfo.isFullForwardTick)
                sounds.PlayNetworked(PlayerSounds.PlayerSoundType.SpinRelease);

            baseState = CharacterMovementState.Rolling;
            isMovingFastEnoughToRoll = true;
            spindashChargeLevel = 0f;
        }
        else if (baseState != CharacterMovementState.Rolling && baseState != CharacterMovementState.SpinCharging)
        {
            spindashChargeLevel = 0f;
        }

        if (!isMovingFastEnoughToRoll && baseState == CharacterMovementState.Rolling)
            baseState = CharacterMovementState.None;
    }

    private void HandleJumpAbilities_Gliding(CharacterInput input, float deltaTime)
    {
        Vector3 aim = input.aimDirection;

        // Cancel gliding upon release
        if (!input.btnJump)
        {
            if (baseState == CharacterMovementState.Gliding)
            {
                if (glide.canMultiGlide)
                    baseState = CharacterMovementState.CanceledJump;
                else
                    baseState = CharacterMovementState.None; // remove jumping state as well to prevent next glide
            }
        }

        // Handle gliding
        if (baseState == CharacterMovementState.Gliding)
        {
            Vector3 gravityUp = -gravityDirection;
            float horizontalSpeed = velocity.AlongPlane(gravityDirection).magnitude;
            Vector3 horizontalAim = aim.AlongPlane(gravityDirection).normalized;
            Vector3 groundForward = horizontalAim.normalized, groundRight = Vector3.Cross(up, aim).normalized;
            Vector3 desiredAcceleration = Vector3.ClampMagnitude(groundForward * input.moveVerticalAxis + groundRight * input.moveHorizontalAxis, 1);

            // gravity cancel and fall control
            velocity.SetAlongAxis(gravityUp, Math.Max(velocity.AlongAxis(gravityUp), -glide.fallSpeedBySpeed.Evaluate(horizontalSpeed)));

            // speed up/slow down
            Vector3 velocityOnGravityPlane = velocity.AlongPlane(gravityUp);
            float targetSpeed = horizontalSpeed + glide.accelerationBySpeed.Evaluate(horizontalSpeed) * Vector3.Dot(velocity.AlongPlane(gravityUp) / horizontalSpeed, desiredAcceleration) * deltaTime;

            velocity.SetAlongPlane(gravityUp, velocityOnGravityPlane * (targetSpeed / horizontalSpeed));

            // turn!
            float turnSpeed = glide.turnSpeedBySpeed.Evaluate(horizontalSpeed) * glide.turnSpeedCurve.Evaluate(Vector3.Angle(velocity.AlongPlane(gravityUp).normalized, desiredAcceleration.normalized)); // in degrees/sec

            velocity.SetAlongPlane(gravityUp, Vector3.RotateTowards(velocity.AlongPlane(gravityUp), desiredAcceleration, turnSpeed * Mathf.Deg2Rad * deltaTime, 0f));

            forward = velocity.AlongPlane(gravityUp);

            // speed clamps
            Vector3 clampedHorizontal = velocity.AlongPlane(gravityUp);
            float clampedHorizontalMag = clampedHorizontal.magnitude;
            if (clampedHorizontalMag < glide.minSpeed)
                velocity.SetAlongPlane(gravityUp, clampedHorizontal * (glide.minSpeed / clampedHorizontalMag));
            else if (clampedHorizontalMag > glide.maxSpeed)
                velocity.SetAlongPlane(gravityUp, clampedHorizontal * (glide.maxSpeed / clampedHorizontalMag));
        }

        // Handle climbing
        if (baseState == CharacterMovementState.Gliding || baseState == CharacterMovementState.Climbing)
        {
            Vector3 wallDirection = baseState == CharacterMovementState.Climbing ? forward.AlongPlane(gravityDirection) : input.aimDirection.AlongPlane(gravityDirection);

            if (Physics.Raycast(transform.position, wallDirection, out RaycastHit hit, 0.5f, blockingCollisionLayers, QueryTriggerInteraction.Ignore))
            {
                forward = -hit.normal.AlongPlane(gravityDirection).normalized;

                if (baseState != CharacterMovementState.Climbing)
                {
                    velocity = Vector3.zero;
                    baseState = CharacterMovementState.Climbing;
                }
                else
                {
                    velocity = (right * input.moveHorizontalAxis + up * input.moveVerticalAxis) * glide.climbSpeed;
                }

                if (hit.distance > 0.01f)
                    velocity -= hit.normal * 5f;
            }
            else
            {
                baseState = CharacterMovementState.CanceledJump;
            }
        }
    }

    private void ApplyGroundStates()
    {
        if (isOnGround && Vector3.Dot(velocity, groundNormal) <= groundEscapeThreshold)
        {
            RemoveStates(CharacterMovementState.Jumped, CharacterMovementState.Thokked, CharacterMovementState.CanceledJump);
        }
    }

    public void SpringUp(float force, Vector3 direction, bool doSpringAbsolutely)
    {
        baseState = CharacterMovementState.None;
        if (doSpringAbsolutely)
            velocity = direction * force;
        else
            velocity.SetAlongAxis(direction, force);
    }

    public void RemoveStates(CharacterMovementState a) => baseState = baseState == a ? CharacterMovementState.None : baseState;
    public void RemoveStates(CharacterMovementState a, CharacterMovementState b) => baseState = (baseState == a || baseState == b) ? CharacterMovementState.None : baseState;
    public void RemoveStates(CharacterMovementState a, CharacterMovementState b, CharacterMovementState c) => baseState = (baseState == a || baseState == b || baseState == c) ? CharacterMovementState.None : baseState;
    public void RemoveStates(CharacterMovementState a, CharacterMovementState b, CharacterMovementState c, CharacterMovementState d) => baseState = (baseState == a || baseState == b || baseState == c || baseState == d) ? CharacterMovementState.None : baseState;

    public bool IsAnyState(CharacterMovementState a) => baseState == a;
    public bool IsAnyState(CharacterMovementState a, CharacterMovementState b) => baseState == a || baseState == b;
    public bool IsAnyState(CharacterMovementState a, CharacterMovementState b, CharacterMovementState c) => baseState == a || baseState == b || baseState == c;
    public bool IsAnyState(CharacterMovementState a, CharacterMovementState b, CharacterMovementState c, CharacterMovementState d) => baseState == a || baseState == b || baseState == c || baseState == d;
    public bool IsAnyState(CharacterMovementState a, CharacterMovementState b, CharacterMovementState c, CharacterMovementState d, CharacterMovementState e) => baseState == a || baseState == b || baseState == c || baseState == d || baseState == e;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        accelCurve = new AnimationCurve();
        inverseAccelCurve = new AnimationCurve();

        float speedFracunits = 0f;
        for (int frame = 0; frame < 75; frame++)
        {
            float curAcceleration = accelStart + speedFracunits * acceleration;
            speedFracunits += 50 * thrustFactor * curAcceleration / 65535f;

            accelCurve.AddKey(frame / 35f, speedFracunits * (35f / 64f));
            inverseAccelCurve.AddKey(speedFracunits * (35f / 64f), frame / 35f);

            if (speedFracunits * (35f / 64f) > topSpeed * 2)
                break;
        }
    }
#endif
}

public static class RaycastExtensions
{
    public static bool RaycastWithDebug(Vector3 start, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask, QueryTriggerInteraction triggerInteraction, bool drawDebug)
    {
        bool result = Physics.Raycast(start, direction, out hit, maxDistance, layerMask, triggerInteraction);

        if (drawDebug)
            Debug.DrawLine(start, start + direction * maxDistance, result ? Color.blue : Color.red);

        return result;
    }
}