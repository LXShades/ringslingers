using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : SyncedObject
{
    private CharacterController controller;

    private new PlayerCamera camera;

    [Header("Movement")]
    public float speed = 8;

    public override void FrameStart()
    {
        camera = FindObjectOfType<PlayerCamera>();
        controller = FindObjectOfType<CharacterController>();
    }

    public override void FrameUpdate()
    {
        // Move based on where we're looking, EZ
        Vector3 direction = camera.transform.forward.Horizontal() * Frame.current.localInput.moveVerticalAxis + camera.transform.right.Horizontal() * Frame.current.localInput.moveHorizontalAxis;
        
        if (direction.magnitude > 1)
            direction.Normalize();

        controller.transform.position += direction * speed * Frame.current.deltaTime;

        // if (local player)
        transform.rotation = Quaternion.Euler(0, camera.horizontalAngle, 0);
    }
}