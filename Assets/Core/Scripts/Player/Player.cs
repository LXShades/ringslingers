using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : SyncedObject
{
    /// <summary>
    /// Name of this player. By default, all players are Fred.
    /// </summary>
    public new string name = "Fred";

    [Header("Look")]
    public float lookVerticalAngle = 0;

    /// <summary>
    /// Current inputs of this player
    /// </summary>
    [HideInInspector] public InputCmds input;

    /// <summary>
    /// Previous inputs of this player
    /// </summary>
    [HideInInspector] public InputCmds lastInput;

    /// <summary>
    /// Player ID
    /// </summary>
    public int playerId;

    /// <summary>
    /// Player movement component
    /// </summary>
    public CharacterMovement movement;

    /// <summary>
    /// Number of rings picked up
    /// </summary>
    public int numRings = 0;

    /// <summary>
    /// Forward vector representing where we're aiming
    /// </summary>
    public Vector3 aimForward;

    public override void FrameAwake()
    {
        movement = GetComponent<CharacterMovement>();
    }

    public override void FrameUpdate()
    {
        float horizontalRads = input.horizontalAim * Mathf.Deg2Rad, verticalRads = input.verticalAim * Mathf.Deg2Rad;

        aimForward = new Vector3(Mathf.Sin(horizontalRads) * Mathf.Cos(verticalRads), -Mathf.Sin(verticalRads), Mathf.Cos(horizontalRads) * Mathf.Cos(verticalRads));
    }
}
