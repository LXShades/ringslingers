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
        horizontalAngle += Frame.local.localInput.lookHorizontalAxis;
        verticalAngle += Frame.local.localInput.lookVerticalAxis;
        horizontalAngle = ((horizontalAngle % 360) + 360) % 360;
    }

    public override void FrameLateUpdate()
    {
        transform.position = currentPlayer.transform.position + Vector3.up;
        transform.rotation = Quaternion.Euler(verticalAngle, horizontalAngle, 0);
    }
}
