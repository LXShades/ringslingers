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

    /// <summary>
    /// How many sync packets will be sent for this object, per second
    /// </summary>
    [Header("SyncedObject")]
    public float syncsPerSecond = 0f;

    // The following Unity functions are disabled to prevent idiot programmers, such as myself, from causing synchronisation errors.
    protected override sealed void Start() { }
    protected override sealed void Update() { }
    protected override sealed void LateUpdate() { }

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

    private static Dictionary<string, Action<object, BinaryWriter>> serializerByType = new Dictionary<string, Action<object, BinaryWriter>>();

    private static Dictionary<string, Action<object, BinaryReader>> deserializerByType = new Dictionary<string, Action<object, BinaryReader>>();

    [StructLayout(LayoutKind.Explicit)]
    public struct IntFloatBridge
    {
        [FieldOffset(0)] public int intValue;
        [FieldOffset(0)] public long longValue;
        [FieldOffset(0)] public float floatValue;
        [FieldOffset(0)] public double doubleValue;
    }

    private static Expression GenerateStreamWriterForType(Expression instance, Expression value, Type type, bool valueIsProperty = true)
    {
        TypeCode code = Type.GetTypeCode(type);

        switch (code)
        {
            case TypeCode.Int32:
            case TypeCode.Int16:
            case TypeCode.UInt32:
            case TypeCode.UInt16:
            case TypeCode.Double:
            case TypeCode.String:
            case TypeCode.Boolean:
                Type closestSystemType = Type.GetType($"System.{code}");
                if (type == closestSystemType)
                    return Expression.Call(instance, typeof(BinaryWriter).GetMethod("Write", new Type[] { type }), value);
                else
                    return Expression.Call(instance, typeof(BinaryWriter).GetMethod("Write", new Type[] { closestSystemType }), Expression.Convert(value, closestSystemType));
            case TypeCode.Single:
                return Expression.Call(instance, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }),
                        new[] {
                            Expression.Field(
                                Expression.MemberInit(
                                    Expression.New(typeof(IntFloatBridge)),
                                    Expression.Bind(
                                        typeof(IntFloatBridge).GetField("floatValue"),
                                        value
                                    )
                                ),
                                "intValue"
                            )
                        }
                    );
            default:
                if (type == typeof(Vector3))
                {
                    return Expression.Block(
                        GenerateStreamWriterForType(instance, Expression.Field(value, "x"), typeof(float)),
                        GenerateStreamWriterForType(instance, Expression.Field(value, "y"), typeof(float)),
                        GenerateStreamWriterForType(instance, Expression.Field(value, "z"), typeof(float))
                    );
                }
                else if (type == typeof(Quaternion))
                {
                    return Expression.Block(
                        GenerateStreamWriterForType(instance, Expression.Field(value, "x"), typeof(float)),
                        GenerateStreamWriterForType(instance, Expression.Field(value, "y"), typeof(float)),
                        GenerateStreamWriterForType(instance, Expression.Field(value, "z"), typeof(float)),
                        GenerateStreamWriterForType(instance, Expression.Field(value, "w"), typeof(float))
                    );
                }
                else if (type.IsSubclassOf(typeof(SyncedObject)))
                {
                    // Write the syncedobject ID
                    return Expression.Condition(
                        Expression.Equal(value, Expression.Constant(null, type)),
                        GenerateStreamWriterForType(instance, Expression.Constant(-1), typeof(int)),
                        GenerateStreamWriterForType(instance, Expression.Property(value, "syncedId"), typeof(int))
                    );
                }
                else if (type.IsValueType && Type.GetTypeCode(type) == TypeCode.Object)
                {
                    return Expression.Call(typeof(SyncedObject).GetMethod("WriteStruct"), new Expression[] { instance, Expression.Convert(value, typeof(object)), Expression.Constant(type) });
                }
                return null;
        }
    }

    private static Expression GenerateStreamReaderForType(Expression instance, Expression target, Type type, bool valueIsProperty = true)
    {
        TypeCode code = Type.GetTypeCode(type);
        Expression reader = null;

        switch (code)
        {
            case TypeCode.Int32:
            case TypeCode.Int16:
            case TypeCode.UInt32:
            case TypeCode.UInt16:
            case TypeCode.Double:
            case TypeCode.String:
            case TypeCode.Boolean:
                if (Type.GetType($"System.{code}") == type)
                    reader = Expression.Call(instance, typeof(BinaryReader).GetMethod($"Read{code}"));
                else
                    reader = Expression.Convert(Expression.Call(instance, typeof(BinaryReader).GetMethod($"Read{code}")), type);
                break;
            case TypeCode.Single:
                reader = Expression.Field(
                            Expression.MemberInit(
                                Expression.New(typeof(IntFloatBridge)),
                                Expression.Bind(
                                    typeof(IntFloatBridge).GetField("intValue"),
                                    Expression.Call(instance, typeof(BinaryReader).GetMethod("ReadInt32"))
                                )
                            ),
                            "floatValue"
                        );
                break;
            default:
                if (type == typeof(Vector3))
                {
                    if (!valueIsProperty && target != null)
                    {
                        return Expression.Block(
                            GenerateStreamReaderForType(instance, Expression.Field(target, "x"), typeof(float)),
                            GenerateStreamReaderForType(instance, Expression.Field(target, "y"), typeof(float)),
                            GenerateStreamReaderForType(instance, Expression.Field(target, "z"), typeof(float))
                        );
                    }
                    else
                    {
                        // can't assign individual elements of a property value, make a new one instead
                        reader = Expression.New(typeof(Vector3).GetConstructor(new Type[] { typeof(float), typeof(float), typeof(float) }), new Expression[]
                                {
                                    GenerateStreamReaderForType(instance, null, typeof(float)),
                                    GenerateStreamReaderForType(instance, null, typeof(float)),
                                    GenerateStreamReaderForType(instance, null, typeof(float)),
                                });
                    }
                }
                else if (type.IsSubclassOf(typeof(SyncedObject)))
                {
                    // read the syncedobject and look it up in Netplay.singleton.syncedObjects
                    ParameterExpression objId = Expression.Variable(typeof(int));

                    var netplaySingleton = Expression.Property(null, typeof(Netplay).GetProperty("singleton"));
                    var getObjFromId = Expression.Condition(
                        Expression.Equal(objId, Expression.Constant(-1)),
                        Expression.Constant(null, type),
                        Expression.Condition(Expression.LessThan(objId, Expression.Property(Expression.Field(netplaySingleton, "syncedObjects"), "Count")),
                            Expression.Convert(
                                Expression.Property(Expression.Field(netplaySingleton, "syncedObjects"), "Item", objId),
                                type
                            ),
                            Expression.Constant(null, type)
                        )
                    );

                    if (target != null)
                    {
                        reader = Expression.Block(
                            new ParameterExpression[] { objId },
                            GenerateStreamReaderForType(instance, objId, typeof(int)),
                            Expression.Assign(
                                target,
                                getObjFromId
                            )
                        );
                    }
                    else
                    {
                        Debug.LogWarning("SyncedObject serialisation currently requires a target");
                        return null;
                    }
                }
                else if (type.IsValueType && Type.GetTypeCode(type) == TypeCode.Object)
                {
                    // read struct
                    reader = Expression.Convert(Expression.Call(typeof(SyncedObject).GetMethod("ReadStruct"), new Expression[] { instance, Expression.Constant(type) }), type);
                }
                else
                {
                    return null;
                }
                break;
        }

        if (target != null && reader != null)
            return Expression.Assign(target, reader);
        else
            return reader;
    }

    static byte[] structBytes = new byte[4096];

    private static System.IntPtr structPtr
    {
        get
        {
            if (_structPtr.ToInt32() == 0)
                _structPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
            return _structPtr;
        }
    }
    private static System.IntPtr _structPtr;

    public static void WriteStruct(BinaryWriter writer, object structure, Type type)
    {
        int structSize = System.Runtime.InteropServices.Marshal.SizeOf(type);
        System.Runtime.InteropServices.Marshal.StructureToPtr(structure, structPtr, false);
        System.Runtime.InteropServices.Marshal.Copy(structPtr, structBytes, 0, structSize);
        writer.Write(structBytes, 0, structSize);
    }

    public static object ReadStruct(BinaryReader reader, Type type)
    {
        int structSize = System.Runtime.InteropServices.Marshal.SizeOf(type);
        reader.Read(structBytes, 0, structSize);
        System.Runtime.InteropServices.Marshal.Copy(structBytes, 0, structPtr, structSize);
        return System.Runtime.InteropServices.Marshal.PtrToStructure(structPtr, type);
    }

    private static Expression CallDebugLog(Expression text)
    {
        return Expression.Call(typeof(Debug).GetMethod("Log", new Type[] { typeof(object) }), new Expression[] { Expression.Call(text, typeof(object).GetMethod("ToString")) });
    }

    private Action<object, BinaryWriter> mySerializer;
    private Action<object, BinaryReader> myDeserializer;

    public virtual void Serialize(BinaryWriter stream)
    {
        // Try to run the serializer for this object
        if (mySerializer == null)
        {
            Type objType = GetType();
            if (!serializerByType.TryGetValue(objType.Name, out mySerializer))
            {
                GenerateSerializers(objType);
                mySerializer = serializerByType[objType.Name];
            }
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
            Type objType = GetType();
            if (!deserializerByType.TryGetValue(objType.Name, out myDeserializer))
            {
                GenerateSerializers(objType);
                myDeserializer = deserializerByType[objType.Name];
            }
        }

        // And run it
        hasCalledStart = stream.ReadByte() > 0;
        myDeserializer.Invoke(this, stream);
    }

    public static void GenerateSerializers(Type objType)
    {
        List<Expression> serializer = new List<Expression>(50);
        List<Expression> deserializer = new List<Expression>(50);
        bool isDebug = true;

        // Retrieve parameters
        ParameterExpression genericTargetParam = Expression.Parameter(typeof(object), "genericTarget");
        ParameterExpression writer = Expression.Parameter(typeof(BinaryWriter), "writer");
        ParameterExpression reader = Expression.Parameter(typeof(BinaryReader), "reader");
        UnaryExpression convert = Expression.Convert(genericTargetParam, objType);
        ParameterExpression target = Expression.Variable(objType, "target");
        ParameterExpression debugMemberName = isDebug ? Expression.Variable(typeof(string), "debugMemberName") : null;

        serializer.Add(Expression.Assign(target, convert));
        deserializer.Add(Expression.Assign(target, convert));

        MemberInfo[] members = objType.GetMembers(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (MemberInfo member in members)
        {
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                continue;

            MemberExpression memberValue = Expression.PropertyOrField(target, member.Name);
            Type variableType = (member.MemberType == MemberTypes.Field ? (member as FieldInfo).FieldType : (member as PropertyInfo).PropertyType);

            if (isDebug)
            {
                serializer.Add(Expression.Assign(debugMemberName, Expression.Constant(member.Name)));
                deserializer.Add(Expression.Assign(debugMemberName, Expression.Constant(member.Name)));
            }

            Expression memberSerializer = GenerateStreamWriterForType(writer, memberValue, variableType, member.MemberType == MemberTypes.Property);
            Expression memberDeserializer = GenerateStreamReaderForType(reader, memberValue, variableType, member.MemberType == MemberTypes.Property);

            if (memberSerializer != null && memberDeserializer != null)
            {
                serializer.Add(memberSerializer);
                deserializer.Add(memberDeserializer);
            }
        }

        // Also write transform
        var getTransform = Expression.MakeMemberAccess(target, objType.GetProperty("transform"));
        var getPosition = Expression.MakeMemberAccess(getTransform, typeof(Transform).GetProperty("position"));
        var getRotation = Expression.MakeMemberAccess(getTransform, typeof(Transform).GetProperty("rotation"));

        serializer.Add(GenerateStreamWriterForType(writer, getPosition, typeof(Vector3)));
        serializer.Add(GenerateStreamWriterForType(writer, getRotation, typeof(Quaternion)));
        deserializer.Add(GenerateStreamReaderForType(reader, getPosition, typeof(Vector3), true));
        deserializer.Add(GenerateStreamReaderForType(reader, getRotation, typeof(Quaternion)));

        // Compile the new function
        BlockExpression serializerBlock, deserializerBlock;

        if (isDebug)
        {
            // Add try/catch blocks to isolate issues de/serializing specific members
            serializerBlock = Expression.Block(
                new ParameterExpression[] { target, debugMemberName },
                Expression.TryCatch(
                    Expression.Block(
                        typeof(void),
                        serializer
                    ),
                    Expression.Catch(
                        typeof(Exception),
                        Expression.Block(
                        CallDebugLog(Expression.Constant("oh no")),
                        CallDebugLog(
                            Expression.Call(
                                typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }),
                                Expression.Constant("Exception serializing object "),
                                Expression.Call(target, typeof(object).GetMethod("ToString")),
                                Expression.Constant(": "),
                                debugMemberName
                            )
                        ))
                    )
                )
            );
            deserializerBlock = Expression.Block(
                new ParameterExpression[] { target, debugMemberName },
                Expression.TryCatch(
                    Expression.Block(
                        typeof(void),
                        deserializer
                    ),
                    Expression.Catch(
                        typeof(Exception),
                        Expression.Block(
                        CallDebugLog(Expression.Constant("oh no")),
                        CallDebugLog(
                            Expression.Call(
                                typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }),
                                Expression.Constant("Exception deserializing object "),
                                Expression.Call(target, typeof(object).GetMethod("ToString")),
                                Expression.Constant(": "),
                                debugMemberName
                            )
                        ))
                    )
                )
            );
        }
        else
        {
            // Just add the code
            serializerBlock = Expression.Block(new ParameterExpression[] { target }, serializer);
            deserializerBlock = Expression.Block(new ParameterExpression[] { target }, deserializer);
        }


        serializerByType.Add(objType.Name, Expression.Lambda<Action<object, BinaryWriter>>(serializerBlock, genericTargetParam, writer).Compile());
        deserializerByType.Add(objType.Name, Expression.Lambda<Action<object, BinaryReader>>(deserializerBlock, genericTargetParam, reader).Compile());
    }

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
