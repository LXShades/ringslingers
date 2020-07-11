using MLAPI.Serialization.Pooled;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
using System.Dynamic;
using JetBrains.Annotations;
using System.Linq;
using System.Runtime.CompilerServices;

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

    public int syncedId => _id;

    private static int nextId = 0;

    public bool isDead
    {
        get; private set;
    }

    private Action<object, BinaryWriter> mySerializer;
    private Action<object, BinaryReader> myDeserializer;

    /// <summary>
    /// How many sync packets will be sent for this object, per second
    /// </summary>
    [Header("SyncedObject")]
    public float syncsPerSecond = 0f;

    // The following Unity functions are disabled to prevent idiot programmers, such as myself, from causing synchronisation errors.
    protected override sealed void Start() { }
    protected override sealed void Update() { }
    protected override sealed void LateUpdate() { }

    #region UnityFuncs
    /// <summary>
    /// Initial setup on creation
    /// </summary>
    protected virtual void Awake()
    {
        _id = nextId++;

        // Register the object to the thingy thing
        if (Netplay.singleton)
            Netplay.singleton.RegisterSyncedObject(this);

        // Call Awake proper real-like
        FrameAwake();
    }

    private void OnDestroy()
    {
        if (!isDead && GameManager.singleton) // GameManager.singleton indicates whether the game is ending
        {
            Debug.LogError("Synced objects should be destroyed with GameManager.DestroyObject");
            Debug.Break();
        }
    }
    #endregion

    #region EventFunctions
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

    /// <summary>
    /// Called when a syncer is requested for this object
    /// </summary>
    public virtual void WriteSyncer(System.IO.Stream stream) { return; }

    /// <summary>
    /// Called when a syncer is delivered to this object
    /// </summary>
    public virtual void ReadSyncer(System.IO.Stream stream) { return; }
    #endregion

    #region CreationFlags
    public void TriggerStartIfCreated()
    { 
        if (!hasCalledStart)
        {
            FrameStart();
            hasCalledStart = true;
        }
    }

    public void FlagAsCreated()
    {
        hasCalledStart = false;
    }

    public void FlagAsDestroyed()
    {
        isDead  = true;
    }

    public void FlagAsRestored()
    {
        isDead = false;
    }
    #endregion

    #region Serialization
    public virtual void Serialize(BinaryWriter stream)
    {
        // Try to run the serializer for this object
        if (mySerializer == null)
        {
            mySerializer = SerializerGenerator.GetOrCreateSerializer(GetType());
        }

        // And run it
        stream.Write((byte)(hasCalledStart ? 1 : 0));
        mySerializer.Invoke(this, stream);
    }

    public virtual void Deserialize(BinaryReader stream)
    {
        // Try to run the serializer for this object
        if (myDeserializer == null)
        {
            myDeserializer = SerializerGenerator.GetOrCreateDeserializer(GetType());
        }

        // And run it
        hasCalledStart = stream.ReadByte() > 0;
        myDeserializer.Invoke(this, stream);
    }
    #endregion

    /// <summary>
    /// Reverts the nextId to a given value
    /// Please don't call this unless you know what you're doing
    /// </summary>
    /// <param name="newNextId"></param>
    public static void RevertNextId(int newNextId)
    {
        Debug.Assert(newNextId <= nextId);
        nextId = newNextId;
    }

    public static int GetNextId()
    {
        return nextId;
    }
}
