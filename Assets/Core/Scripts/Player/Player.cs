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
}
