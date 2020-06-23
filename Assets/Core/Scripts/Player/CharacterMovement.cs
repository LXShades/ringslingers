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
    public float speed = 8;
    public float topSpeed = 36;
    public float gravity = 0.5f;

    public float accelStart = 96;
    public float acceleration = 40;
    public float friction = 0.90625f;
    public float thrustFactor = 5;
    public float airAccelerationMultiplier = 0.25f;
    public float stopSpeed = (1f / 64f);

    public float jumpSpeed = (39f / 4f);
    public float jumpFactor = 1;

    [Header("Scaling")]
    public float fracunitsPerM = 64;

    private Vector3 velocity;

    private bool isOnGround = false;

    public override void FrameStart()
    {
        camera = FindObjectOfType<PlayerCamera>();
        controller = FindObjectOfType<CharacterController>();
        player = GetComponent<Player>();
    }

    public override void FrameUpdate()
    {
        if (Frame.local.deltaTime == 0)
        {
            return;
        }

        // Move based on where we're looking, EZ
        Vector3 direction = camera.transform.forward.Horizontal().normalized * player.input.moveVerticalAxis + camera.transform.right.Horizontal().normalized * player.input.moveHorizontalAxis;
        
        if (direction.magnitude > 1)
            direction.Normalize();

        // Friction
        if (velocity.magnitude > 0 && isOnGround)
        {
            velocity.SetHorizontal(velocity.Horizontal() * Mathf.Pow(friction, Frame.local.deltaTime * 35f));
        }

        // Acceleration
        float speed = velocity.Horizontal().magnitude; // todo: use rmomentum
        float scale = 1;// transform.localScale.x;
        float currentAcceleration = accelStart + speed * acceleration * Mathf.Pow(1, Frame.local.deltaTime * 35f); // divide by scale in the real game

        if (!isOnGround)
            currentAcceleration *= airAccelerationMultiplier;

        velocity += direction * (50 * thrustFactor * currentAcceleration * 35f / 65536f * Frame.local.deltaTime);

        // Gravity
        velocity += new Vector3(0, -1, 0) * (gravity * 35f * Frame.local.deltaTime);

        // Top speed clamp
        if (velocity.Horizontal().magnitude > topSpeed)
        {
            velocity.SetHorizontal(velocity.Horizontal() * topSpeed / velocity.magnitude);

            if (lastStoppedTime != 0)
            {
                Debug.Log($"Reached top speed in {Frame.local.time - lastStoppedTime}");
                lastStoppedTime = 0;
            }
        }

        // Stop speed
        if (direction.sqrMagnitude == 0 && velocity.Horizontal().magnitude < stopSpeed)
        {
            velocity.SetHorizontal(Vector3.zero);
        }

        // Perform final movement and collision
        CollisionFlags collision = controller.Move(velocity * (35 * Frame.local.deltaTime / fracunitsPerM));

        if (collision.HasFlag(CollisionFlags.CollidedBelow))
        {
            velocity.y = Mathf.Max(velocity.y, 0);
            isOnGround = true;
        }
        else
        {
            isOnGround = false;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            velocity.y = jumpSpeed * jumpFactor;
            Physics.SyncTransforms();
        }
        // if release, divide jump speed by 2

        if (Input.GetKeyDown(KeyCode.E))
        {
            Application.targetFrameRate = 35;
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.targetFrameRate = 144;
        }

        transform.rotation = Quaternion.Euler(0, camera.horizontalAngle, 0);

        if (velocity.Horizontal().magnitude < 0.1f)
        {
            lastStoppedTime = Frame.local.time;
        }
    }
    float lastStoppedTime;
}