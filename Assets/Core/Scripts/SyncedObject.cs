using MLAPI.Serialization.Pooled;
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

    private int _id;

    public int id => _id;

    private static int nextId = 1;

    /// <summary>
    /// How many sync packets will be sent for this object, per second
    /// </summary>
    [Header("SyncedObject")]
    public float syncsPerSecond = 0f;

    // The following Unity functions are disabled to prevent idiot programmers, such as myself, from causing synchronisation errors.
    protected override sealed void Start() { }
    protected override sealed void Update() { }
    protected override sealed void LateUpdate() { }

    protected virtual void Awake()
    {
        // Register the object to the thingy thing
        GameManager.singleton.syncedObjects.Add(this);

        _id = nextId++;

        // Call Awake proper real-like
        FrameAwake();
    }

    /// <summary>
    /// Called when an object is created and its synced _stuff_ is initialized
    /// </summary>
    public virtual void FrameAwake() { return; }

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

    public virtual void WriteSyncer(System.IO.Stream stream) { return; }

    public virtual void ReadSyncer(System.IO.Stream stream) { return; }

    public void TriggerStartIfCreated()
    { 
        if (!hasCalledStart)
        {
            FrameStart();
            hasCalledStart = true;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.singleton)
        {
            GameManager.singleton.syncedObjects.Remove(this);
            //Debug.Log("Unregistered synced object");
        }
    }
}
