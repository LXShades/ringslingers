using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SyncedObjectBase : MonoBehaviour
{
    protected virtual void Start() { }

    protected virtual void Update() { }

    protected virtual void LateUpdate() { }

}

public abstract class SyncedObject : SyncedObjectBase
{
    private bool hasCalledStart = false;

    // The following Unity functions are disabled to prevent idiot programmers, such as myself, from causing synchronisation errors.
    protected override sealed void Start() { }
    protected override sealed void Update() { }
    protected override sealed void LateUpdate() { }

    /// <summary>
    /// Called before the first frame where the object exists begins
    /// </summary>
    public virtual void FrameStart() { return; }

    /// <summary>
    /// Called during a synchronised frame update
    /// </summary>
    public virtual void FrameUpdate() { return; }

    /// <summary>
    /// Called during a synchronised late frame update (after all other updates)
    /// </summary>
    public virtual void FrameLateUpdate() { return; }

    public void TriggerStartIfCreated()
    { 
        if (!hasCalledStart)
        {
            FrameStart();
            hasCalledStart = true;
        }
    }
}
