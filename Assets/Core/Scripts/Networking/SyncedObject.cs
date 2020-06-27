using MLAPI.Serialization.Pooled;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;

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
        if (Netplay.singleton)
            Netplay.singleton.RegisterSyncedObject(this);

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

    public virtual void Serialize(Stream data)
    {
        Type objType = GetType();
        FieldInfo[] fields = objType.GetFields();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        BinaryFormatter formatter = new BinaryFormatter();

        using (BinaryWriter stream = new BinaryWriter(data, System.Text.Encoding.UTF8, true))
        {
            object val;
            Type valType;

            for (int i = 0; i < fields.Length + properties.Length; i++)
            {
                if (i < fields.Length)
                {
                    val = fields[i].GetValue(this);
                    valType = fields[i].FieldType;
                }
                else
                {
                    val = properties[i - fields.Length].GetValue(this);
                    valType = properties[i - fields.Length].PropertyType;
                }

                switch (Type.GetTypeCode(valType))
                {
                    case TypeCode.Int32:
                        stream.Write((int)val);
                        break;
                    case TypeCode.UInt32:
                        stream.Write((uint)val);
                        break;
                    case TypeCode.Single:
                        unsafe // apparently floats cause allocations which we don't want or need during this process
                        {
                            int valAsInt = 0;
                            *((float*)&valAsInt) = (float)val;
                            stream.Write(valAsInt);
                        }
                        break;
                    case TypeCode.Double:
                        unsafe // apparently floats cause allocations which we don't want or need during this process
                        {
                            long valAsLong = 0;
                            *((double*)&valAsLong) = (double)val;
                            stream.Write(valAsLong);
                        }
                        break;
                    case TypeCode.String:
                        stream.Write((string)val);
                        break;
                    case TypeCode.Boolean:
                        stream.Write((bool)val);
                        break;
                    default:
                        // Non-numeric type
                        if (valType == typeof(Vector3))
                        {
                            Vector3 asVec = (Vector3)val;
                            stream.Write(asVec.x);
                            stream.Write(asVec.y);
                            stream.Write(asVec.z);
                        }
                        break;
                }
            }

            // Write transform
            stream.Write(transform.position.x);
            stream.Write(transform.position.y);
            stream.Write(transform.position.z);
        }
    }

    public virtual void Deserialize(Stream data)
    {
        Type objType = GetType();
        FieldInfo[] fields = objType.GetFields();
        PropertyInfo[] properties = objType.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        BinaryFormatter formatter = new BinaryFormatter();

        using (BinaryReader stream = new BinaryReader(data, System.Text.Encoding.UTF8, true))
        {
            Type valType;
            dynamic fieldOrProperty;

            for (int i = 0; i < fields.Length + properties.Length; i++)
            {
                if (i < fields.Length)
                {
                    valType = fields[i].FieldType;
                    fieldOrProperty = fields[i];
                }
                else
                {
                    valType = properties[i - fields.Length].PropertyType;
                    fieldOrProperty = properties[i - fields.Length];
                }

                switch (Type.GetTypeCode(valType))
                {
                    case TypeCode.Int32:
                        fieldOrProperty.SetValue(this, stream.ReadInt32());
                        break;
                    case TypeCode.UInt32:
                        fieldOrProperty.SetValue(this, stream.ReadUInt32());
                        break;
                    case TypeCode.Single:
                        fieldOrProperty.SetValue(this, stream.ReadSingle());
                        break;
                    case TypeCode.Double:
                        fieldOrProperty.SetValue(this, stream.ReadDouble());
                        break;
                    case TypeCode.String:
                        fieldOrProperty.SetValue(this, stream.ReadString());
                        break;
                    case TypeCode.Boolean:
                        fieldOrProperty.SetValue(this, stream.ReadBoolean());
                        break;
                    default:
                        // Non-numeric type
                        if (valType == typeof(Vector3))
                        {
                            Vector3 asVec;
                            asVec.x = stream.ReadSingle();
                            asVec.y = stream.ReadSingle();
                            asVec.z = stream.ReadSingle();
                            fieldOrProperty.SetValue(this, asVec);
                        }
                        break;
                }
            }

            // Write transform
            Vector3 position;
            position.x = stream.ReadSingle();
            position.y = stream.ReadSingle();
            position.z = stream.ReadSingle();
            transform.position = position;
        }

        // Positions have changed
        Physics.SyncTransforms();
    }

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
        if (Netplay.singleton)
            Netplay.singleton.UnregisterSyncedObject(this);
    }
}
