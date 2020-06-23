using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCamera : SyncedObject
{
    /// <summary>
    /// Player we're currently following
    /// </summary>
    public Player currentPlayer;

    /// <summary>
    /// The height of the camera relative to the player's feet
    /// </summary>
    public float eyeHeight = 0.6f;

    /// <summary>
    /// Horizontal look angle in degrees
    /// </summary>
    public float horizontalAngle = 0;

    /// <summary>
    /// Vertiacl look angle in degrees
    /// </summary>
    public float verticalAngle = 0;

    public override void FrameStart()
    {
        currentPlayer = FindObjectOfType<Player>(); // temporary
    }

    public override void FrameUpdate()
    {
        horizontalAngle += currentPlayer.input.lookHorizontalAxis;
        verticalAngle += currentPlayer.input.lookVerticalAxis;
        horizontalAngle = ((horizontalAngle % 360) + 360) % 360;
    }

    public override void FrameLateUpdate()
    {
        transform.position = currentPlayer.transform.position + Vector3.up * eyeHeight;
        transform.rotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0);
    }
}
