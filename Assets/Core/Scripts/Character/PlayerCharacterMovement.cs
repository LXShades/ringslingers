using System;
using UnityEngine;

public class PlayerCharacterMovement : CharacterMovement
{
    public enum JumpAbility
    {
        Thok,
        Glide
    }

    private Character player;
    private PlayerSounds sounds;

    [Header("[PlayerCharacterMovement] Stats (FRACUNITS)")]
    public float accelStart = 96;
    public float acceleration = 40;
    public float thrustFactor = 5;
    public float topSpeed = 36;

    public float friction = 0.90625f;
    public float stopSpeed = (1f / 64f);

    public float jumpSpeed = (39f / 4f);
    public float jumpFactor = 1;

    public float airAccelerationMultiplier = 0.25f;

    [Header("3D movement")]
    public float wallRunSpeedThreshold = 10f;
    public float wallRunRotationResetSpeed = 180f;
    public Transform rotateableModel;

    public LayerMask landableCollisionLayers;

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

    /*public Vector3 up
    {
        get => _up;
        set
        {
            // change look rotation with wall run rotation motion if wallRunCameraAssist is enabled. Recompressed up to prevent drift when saving/loading quantized state
            if (wallRunCameraAssist && player != null && Netplay.singleton.localPlayer == player)
            {
                GameTicker.singleton.localPlayerInput.aimDirection = Quaternion.FromToRotation(CharacterState.RecompressUp(_up), CharacterState.RecompressUp(value)) * GameTicker.singleton.localPlayerInput.aimDirection;
            }

            _up = value;
        }
    }
    private Vector3 _up = Vector3.up;*/

    void Awake()
    {
        player = GetComponent<Character>();
        sounds = GetComponent<PlayerSounds>();
    }

    public void TickMovement(float deltaTime, PlayerInput input, bool isRealtime = true)
    {
        Physics.SyncTransforms();

        // Check whether on ground
        CalculateGroundInfo(out GroundInfo groundInfo);

        isOnGround = groundInfo.isOnGround;
        groundNormal = groundInfo.normal;

        forward = input.aimDirection;

        // Apply grounding effects straight away so we can be more up-to-date with wallrunning stuff
        ApplyGroundStates();

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
        ApplyCharacterGravity(groundInfo, deltaTime);

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
        //ApplyRotation(deltaTime, input);

        // Final movement
        ApplyCharacterVelocity(groundInfo, deltaTime, isRealtime);

        // Set final rotation
        transform.rotation = Quaternion.LookRotation(forward.AlongPlane(up), up);
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
            else if (state.HasFlag(State.Jumped) && (player == null || !player.isHoldingFlag))
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

            if (!isOnGround) // experiment: we'll let you release a spindash in the air. but you can't do it repeatedly
                state &= ~State.Rolling;

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
        Vector3 targetUp = groundNormal;

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

    private void ApplyGroundStates()
    {
        if (isOnGround)
        {
            state &= ~(State.Jumped | State.Thokked | State.CanceledJump);
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