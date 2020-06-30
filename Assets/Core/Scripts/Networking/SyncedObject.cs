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
        _id = nextId++;

        // Register the object to the thingy thing
        if (Netplay.singleton)
            Netplay.singleton.RegisterSyncedObject(this);

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

    private static Dictionary<string, Action<object, BinaryWriter>> serializerByType = new Dictionary<string, Action<object, BinaryWriter>>();

    private static Dictionary<string, Action<object, BinaryReader>> deserializerByType = new Dictionary<string, Action<object, BinaryReader>>();

    private static MethodCallExpression CallStreamWriterForType(Expression instance, Expression value, Type type)
    {
        TypeCode code = Type.GetTypeCode(type);

        switch (code)
        {
            case TypeCode.Int32:
            case TypeCode.Int16:
            case TypeCode.UInt32:
            case TypeCode.UInt16:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.String:
            case TypeCode.Boolean:
                Type closestSystemType = Type.GetType($"System.{code}");
                if (type == closestSystemType)
                    return Expression.Call(instance, typeof(BinaryWriter).GetMethod("Write", new Type[] { type }), value);
                else
                    return Expression.Call(instance, typeof(BinaryWriter).GetMethod("Write", new Type[] { closestSystemType }), Expression.Convert(value, closestSystemType));
            default:
                return null;
        }
    }

    private static BinaryExpression CallStreamReaderForType(Expression instance, Expression target, Type type)
    {
        return Expression.Assign(target, CallStreamReaderForType(instance, type));
    }

    private static Expression CallStreamReaderForType(Expression instance, Type type)
    {
        TypeCode code = Type.GetTypeCode(type);
        switch (code)
        {
            case TypeCode.Int32:
            case TypeCode.Int16:
            case TypeCode.UInt32:
            case TypeCode.UInt16:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.String:
            case TypeCode.Boolean:
                if (Type.GetType($"System.{code}") == type)
                    return Expression.Call(instance, typeof(BinaryReader).GetMethod($"Read{code}"));
                else
                {
                    var converted = Expression.Convert(Expression.Call(instance, typeof(BinaryReader).GetMethod($"Read{code}")), type);
                    return converted;
                }
            default:
                return null; // lmao i dunno what to do haha
        }
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
        myDeserializer.Invoke(this, stream);

        // Positions have changed
        Physics.SyncTransforms();
    }

    public static void GenerateSerializers(Type objType)
    {
        List<Expression> serializer = new List<Expression>(50);
        List<Expression> deserializer = new List<Expression>(50);

        // Retrieve parameters
        ParameterExpression genericTargetParam = Expression.Parameter(typeof(object), "genericTarget");
        ParameterExpression writer = Expression.Parameter(typeof(BinaryWriter), "writer");
        ParameterExpression reader = Expression.Parameter(typeof(BinaryReader), "reader");
        UnaryExpression convert = Expression.Convert(genericTargetParam, objType);
        ParameterExpression target = Expression.Variable(objType, "target");
        ParameterExpression tempInt = Expression.Variable(typeof(int), "tempInt");
        MethodInfo writeStructFunc = typeof(SyncedObject).GetMethod("WriteStruct");
        MethodInfo readStructFunc = typeof(SyncedObject).GetMethod("ReadStruct");

        serializer.Add(Expression.Assign(target, convert));
        deserializer.Add(Expression.Assign(target, convert));

        MemberInfo[] members = objType.GetMembers(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (MemberInfo member in members)
        {
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                continue;

            MemberExpression getValue = Expression.PropertyOrField(target, member.Name);
            Type variableType = (member.MemberType == MemberTypes.Field ? (member as FieldInfo).FieldType : (member as PropertyInfo).PropertyType);

            switch (Type.GetTypeCode(variableType))
            {
                case TypeCode.Int32:
                case TypeCode.Int16:
                case TypeCode.UInt32:
                case TypeCode.UInt16:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.String:
                case TypeCode.Boolean:
                    serializer.Add(CallStreamWriterForType(writer, getValue, variableType));
                    deserializer.Add(CallStreamReaderForType(reader, getValue, variableType)); // um, this function does it automatically I guess
                    break;
                default:
                    if (variableType == typeof(Vector3))
                    {
                        serializer.Add(CallStreamWriterForType(writer, Expression.MakeMemberAccess(getValue, typeof(Vector3).GetMember("x")[0]), typeof(float)));
                        serializer.Add(CallStreamWriterForType(writer, Expression.MakeMemberAccess(getValue, typeof(Vector3).GetMember("y")[0]), typeof(float)));
                        serializer.Add(CallStreamWriterForType(writer, Expression.MakeMemberAccess(getValue, typeof(Vector3).GetMember("z")[0]), typeof(float)));

                        if (member.MemberType == MemberTypes.Field)
                        {
                            // assign each element of the vector and call it a day
                            deserializer.Add(CallStreamReaderForType(reader, Expression.MakeMemberAccess(getValue, typeof(Vector3).GetMember("x")[0]), typeof(float)));
                            deserializer.Add(CallStreamReaderForType(reader, Expression.MakeMemberAccess(getValue, typeof(Vector3).GetMember("y")[0]), typeof(float)));
                            deserializer.Add(CallStreamReaderForType(reader, Expression.MakeMemberAccess(getValue, typeof(Vector3).GetMember("z")[0]), typeof(float)));
                        }
                        else
                        {
                            // can't assign individual elements of a property struct
                            Expression newVector = Expression.New(typeof(Vector3).GetConstructor(new Type[] { typeof(float), typeof(float), typeof(float) }), new Expression[] {
                                CallStreamReaderForType(reader, typeof(float)),
                                CallStreamReaderForType(reader, typeof(float)),
                                CallStreamReaderForType(reader, typeof(float))
                            });

                            deserializer.Add(Expression.Assign(getValue, newVector));
                        }
                    }
                    else if (variableType.IsSubclassOf(typeof(SyncedObject)))
                    {
                        // Write the syncedobject ID
                        serializer.Add(
                            Expression.Condition(
                                Expression.Equal(getValue, Expression.Constant(null, variableType)),
                                CallStreamWriterForType(writer, Expression.Constant(-1), typeof(int)),
                                CallStreamWriterForType(writer, Expression.Property(getValue, "syncedId"), typeof(int))
                            )
                        );
                        
                        // read the syncedobject and look it up in Netplay.singleton.syncedObjects
                        deserializer.Add(
                            CallStreamReaderForType(reader, tempInt, typeof(int))
                        );

                        var netplaySingleton = Expression.Property(null, typeof(Netplay).GetProperty("singleton"));

                        deserializer.Add(
                            Expression.Assign(
                                getValue,
                                Expression.Condition(
                                    Expression.Equal(tempInt, Expression.Constant(-1)),
                                    Expression.Constant(null, variableType),
                                    Expression.Convert(
                                        Expression.Property(Expression.Field(netplaySingleton, "syncedObjects"), "Item", tempInt),
                                        variableType
                                    )
                                )
                            )
                        );
                    }
                    else if (variableType.IsValueType && Type.GetTypeCode(variableType) == TypeCode.Object)
                    {
                        serializer.Add(Expression.Call(writeStructFunc, new Expression[] { writer, Expression.Convert(getValue, typeof(object)), Expression.Constant(variableType) }));
                        deserializer.Add(Expression.Assign(getValue, Expression.Convert(Expression.Call(readStructFunc, new Expression[] { reader, Expression.Constant(variableType) }), variableType)));
                    }
                    break;
            }
        }

        // Also write transform
        var getTransform = Expression.MakeMemberAccess(target, objType.GetProperty("transform"));
        var getPosition = Expression.MakeMemberAccess(getTransform, typeof(Transform).GetProperty("position"));

        serializer.Add(CallStreamWriterForType(writer, Expression.MakeMemberAccess(getPosition, typeof(Vector3).GetField("x")), typeof(float)));
        serializer.Add(CallStreamWriterForType(writer, Expression.MakeMemberAccess(getPosition, typeof(Vector3).GetField("y")), typeof(float)));
        serializer.Add(CallStreamWriterForType(writer, Expression.MakeMemberAccess(getPosition, typeof(Vector3).GetField("z")), typeof(float)));

        Expression newPosition = Expression.New(typeof(Vector3).GetConstructor(new Type[] { typeof(float), typeof(float), typeof(float) }), new Expression[] {
                                CallStreamReaderForType(reader, typeof(float)),
                                CallStreamReaderForType(reader, typeof(float)),
                                CallStreamReaderForType(reader, typeof(float))
                            });
        deserializer.Add(Expression.Assign(getPosition, newPosition));

        // Compile the new function
        BlockExpression serializerBlock = Expression.Block(new ParameterExpression[] { target, tempInt }, serializer);
        BlockExpression deserializerBlock = Expression.Block(new ParameterExpression[] { target, tempInt }, deserializer);

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

    private void OnDestroy()
    {
        if (Netplay.singleton)
            Netplay.singleton.UnregisterSyncedObject(this);
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
