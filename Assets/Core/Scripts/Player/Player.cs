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

    public override void FrameAwake()
    {
        movement = GetComponent<CharacterMovement>();
    }
}
