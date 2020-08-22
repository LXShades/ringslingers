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
    protected virtual void Awake() { }

    protected virtual void Start() { }

    protected virtual void Update() { }

    protected virtual void LateUpdate() { }

}

/// <summary>
/// A WorldObject component is a part of a worldobject that can be ticked and rewinded
/// </summary>
[ExecuteInEditMode]
public abstract class WorldObjectComponent : SyncedObjectBase
{
    private Action<object, BinaryWriter> mySerializer;
    private Action<object, BinaryReader> myDeserializer;

    /// <summary>
    /// The WorldObject that this is a part of
    /// </summary>
    public WorldObject worldObject {get; private set;}

    /// <summary>
    /// How many sync packets will be sent for this object, per second
    /// </summary>
    [Header("WorldObject")]
    public float syncsPerSecond = 0f;
#if UNITY_EDITOR
    [SerializeField, TextArea] private string clonerInfo;
#endif

#if UNITY_EDITOR
    public void OnEnable()
    {
        clonerInfo = ClonerGenerator.GetClonerInfo(GetType()).Replace(", ", "\n");
    }
#endif

    // The following Unity functions are disabled to prevent idiot programmers, such as myself, from causing synchronisation errors.
    protected override sealed void Start() { }
    protected override sealed void Update() { }
    protected override sealed void LateUpdate() { }
    protected override sealed void Awake()
    {
        worldObject = GetComponentInParent<WorldObject>();

        if (worldObject == null)
            Debug.LogError($"WorldObjectComponent needs a WorldObject parent! {name}");
    }

    #region EventFunctions
    /// <summary>
    /// Called when an object is created and its synced _stuff_ is initialized
    /// </summary>
    public virtual void WorldAwake() { return; }

    /// <summary>
    /// Called before the first frame where the object exists begins
    /// </summary>
    public virtual void WorldStart() { return; }

    /// <summary>
    /// Called during a synchronised frame update
    /// </summary>
    public virtual void WorldUpdate(float deltaTime) { return; }

    /// <summary>
    /// Called during a synchronised late frame update (after all other updates)
    /// </summary>
    public virtual void WorldLateUpdate(float deltaTime) { return; }

    /// <summary>
    /// Called when a syncer is requested for this object
    /// </summary>
    public virtual void WriteSyncer(System.IO.Stream stream) { return; }

    /// <summary>
    /// Called when a syncer is delivered to this object
    /// </summary>
    public virtual void ReadSyncer(System.IO.Stream stream) { return; }
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
        myDeserializer.Invoke(this, stream);
    }
    #endregion

    public void CloneFrom(WorldObjectComponent source)
    {
        Action<object, object> cloner = ClonerGenerator.GetOrCreateCloner(GetType());
        if (cloner != null)
        {
            cloner.Invoke(this, source);
        }
    }
}
