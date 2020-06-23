using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ring : SyncedObject
{
    [Header("Ring")]
    [Tooltip("Speed that this ring spins at, in degrees per second")] public float spinSpeed = 180;

    public override void FrameUpdate()
    {
        base.FrameUpdate();

        // Spinny spin
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(0, spinSpeed * Frame.local.deltaTime, 0));
    }
}
