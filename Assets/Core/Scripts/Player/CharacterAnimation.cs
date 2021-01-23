﻿using UnityEngine;

public class CharacterAnimation : MonoBehaviour
{
    private CharacterMovement movement;
    private Player player;
    private Animator animation;

    [Header("Body parts")]
    public Transform root;
    public Transform torso;
    public Transform head;

    [Header("Settings")]
    public float legTurnDegreesPerSecond = 360f;
    public float fallTiltDegreesPerSecond = 50f;
    public float fallTiltMaxDegrees = 20f;

    private Quaternion lastRootRotation = Quaternion.identity;
    private Vector3 lastCharacterUp = Vector3.up;

    private void Start()
    {
        movement = GetComponentInParent<CharacterMovement>();
        player = GetComponentInParent<Player>();
        animation = GetComponentInParent<Animator>();
    }

    private void Update()
    {
        Vector3 groundVelocity = movement.groundVelocity;
        float forwardSpeedMultiplier = Vector3.Dot(transform.forward.Horizontal(), groundVelocity) <= 0f ? -1 : 1;

        animation.SetFloat("HorizontalSpeed", groundVelocity.magnitude);
        animation.SetFloat("HorizontalForwardSpeed", groundVelocity.magnitude * forwardSpeedMultiplier);
        animation.SetBool("IsOnGround", movement.isOnGround);
        animation.SetBool("IsRolling", !movement.isOnGround && (movement.state & (CharacterMovement.State.Jumped | CharacterMovement.State.Rolling)) != 0);
        animation.SetBool("IsSpringing", !movement.isOnGround && movement.velocity.y > 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsFreeFalling", !movement.isOnGround && movement.velocity.y < 0 && (movement.state & CharacterMovement.State.Jumped) == 0);
        animation.SetBool("IsHurt", (movement.state & CharacterMovement.State.Pained) != 0);
    }

    private void LateUpdate()
    {
        Vector3 groundVelocity = movement.groundVelocity;
        Vector3 characterUp = movement.up;

        // Turn body towards look direction
        if (movement.isOnGround && groundVelocity.magnitude > 0.2f)
        {
            Vector3 runForward = groundVelocity;

            if (Vector3.Dot(transform.forward.Horizontal(), runForward) <= 0f)
            {
                runForward = -runForward;
            }

            Quaternion forwardToVelocity = Quaternion.LookRotation(runForward, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(transform.forward.AlongPlane(characterUp), characterUp));

            root.rotation = Quaternion.RotateTowards(lastRootRotation, forwardToVelocity * root.rotation, Time.deltaTime * legTurnDegreesPerSecond);
            torso.rotation = Quaternion.Inverse(forwardToVelocity) * torso.rotation;

            lastRootRotation = root.rotation;
        }

        // think of this as rotation = originalRotation - forwardRotation + newHeadForwardRotation
        head.transform.rotation = Quaternion.LookRotation(player.input.aimDirection, characterUp) * Quaternion.Inverse(Quaternion.LookRotation(torso.forward.AlongPlane(characterUp), characterUp)) * head.transform.rotation;

        lastCharacterUp = characterUp;
    }
}
