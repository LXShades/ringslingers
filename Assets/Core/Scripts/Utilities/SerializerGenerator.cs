using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public class SerializerGenerator
{
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

    private delegate void WriteStructFunction(BinaryWriter writer, object structure, Type type);
    private delegate object ReadStructFunction(BinaryReader reader, Type type);

    private delegate void WriteListFunction<T>(BinaryWriter writer, List<T> list);
    private delegate object ReadListFunction(BinaryReader reader, Type type);

    private static MethodInfo funcWriteStruct;
    private static MethodInfo funcReadStruct;

    private static MethodInfo funcWriteList;
    private static MethodInfo funcReadList;

    static SerializerGenerator()
    {
        funcWriteStruct = ((WriteStructFunction)WriteStruct).GetMethodInfo();
        funcReadStruct = ((ReadStructFunction)ReadStruct).GetMethodInfo();
    }

    private static Expression GenerateStreamWriterForType(Expression stream, Expression value, Type type, bool valueIsProperty = true)
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
                    return Expression.Call(stream, typeof(BinaryWriter).GetMethod("Write", new Type[] { type }), value);
                else
                    return Expression.Call(stream, typeof(BinaryWriter).GetMethod("Write", new Type[] { closestSystemType }), Expression.Convert(value, closestSystemType));
            case TypeCode.Single:
                return Expression.Call(stream, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }),
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
                        GenerateStreamWriterForType(stream, Expression.Field(value, "x"), typeof(float)),
                        GenerateStreamWriterForType(stream, Expression.Field(value, "y"), typeof(float)),
                        GenerateStreamWriterForType(stream, Expression.Field(value, "z"), typeof(float))
                    );
                }
                else if (type == typeof(Quaternion))
                {
                    return Expression.Block(
                        GenerateStreamWriterForType(stream, Expression.Field(value, "x"), typeof(float)),
                        GenerateStreamWriterForType(stream, Expression.Field(value, "y"), typeof(float)),
                        GenerateStreamWriterForType(stream, Expression.Field(value, "z"), typeof(float)),
                        GenerateStreamWriterForType(stream, Expression.Field(value, "w"), typeof(float))
                    );
                }
                else if (type.IsSubclassOf(typeof(SyncedObject)))
                {
                    // Write the syncedobject ID
                    return Expression.Condition(
                        Expression.Equal(value, Expression.Constant(null, type)),
                        GenerateStreamWriterForType(stream, Expression.Constant(-1), typeof(int)),
                        GenerateStreamWriterForType(stream, Expression.Property(value, "syncedId"), typeof(int))
                    );
                }
                else if (type.IsValueType && Type.GetTypeCode(type) == TypeCode.Object)
                {
                    return Expression.Call(funcWriteStruct, stream, Expression.Convert(value, typeof(object)), Expression.Constant(type));
                }
                return null;
        }
    }

    private static Expression GenerateStreamReaderForType(Expression stream, Expression target, Type type, bool valueIsProperty = true)
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
                    reader = Expression.Call(stream, typeof(BinaryReader).GetMethod($"Read{code}"));
                else
                    reader = Expression.Convert(Expression.Call(stream, typeof(BinaryReader).GetMethod($"Read{code}")), type);
                break;
            case TypeCode.Single:
                reader = Expression.Field(
                            Expression.MemberInit(
                                Expression.New(typeof(IntFloatBridge)),
                                Expression.Bind(
                                    typeof(IntFloatBridge).GetField("intValue"),
                                    Expression.Call(stream, typeof(BinaryReader).GetMethod("ReadInt32"))
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
                            GenerateStreamReaderForType(stream, Expression.Field(target, "x"), typeof(float)),
                            GenerateStreamReaderForType(stream, Expression.Field(target, "y"), typeof(float)),
                            GenerateStreamReaderForType(stream, Expression.Field(target, "z"), typeof(float))
                        );
                    }
                    else
                    {
                        // can't assign individual elements of a property value, make a new one instead
                        reader = Expression.New(typeof(Vector3).GetConstructor(new Type[] { typeof(float), typeof(float), typeof(float) }),
                                    GenerateStreamReaderForType(stream, null, typeof(float)),
                                    GenerateStreamReaderForType(stream, null, typeof(float)),
                                    GenerateStreamReaderForType(stream, null, typeof(float))
                                );
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
                            GenerateStreamReaderForType(stream, objId, typeof(int)),
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
                else if (type.IsSubclassOf(typeof(List<>)))
                {
                    return Expression.Call(funcReadList, stream, target);
                }
                else if (type.IsValueType && Type.GetTypeCode(type) == TypeCode.Object)
                {
                    // read struct
                    reader = Expression.Convert(Expression.Call(funcReadStruct, stream, Expression.Constant(type)), type);
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
    private static IntPtr structPtr
    {
        get
        {
            if (_structPtr.ToInt32() == 0)
                _structPtr = Marshal.AllocHGlobal(4096);
            return _structPtr;
        }
    }
    private static IntPtr _structPtr;

    public static void WriteStruct(BinaryWriter writer, object structure, Type type)
    {
        int structSize = Marshal.SizeOf(type);
        Marshal.StructureToPtr(structure, structPtr, false);
        Marshal.Copy(structPtr, structBytes, 0, structSize);
        writer.Write(structBytes, 0, structSize);
    }

    public static object ReadStruct(BinaryReader reader, Type type)
    {
        int structSize = Marshal.SizeOf(type);
        reader.Read(structBytes, 0, structSize);
        Marshal.Copy(structBytes, 0, structPtr, structSize);
        return Marshal.PtrToStructure(structPtr, type);
    }

    private static Expression CallDebugLog(Expression text)
    {
        return Expression.Call(typeof(Debug).GetMethod("Log", new[] { typeof(object) }), Expression.Call(text, typeof(object).GetMethod("ToString")));
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

    public static Action<object, BinaryWriter> GetOrCreateSerializer(Type type)
    {
        Action<object, BinaryWriter> serializer;
        if (!serializerByType.TryGetValue(type.Name, out serializer))
        {
            GenerateSerializers(type);
            serializer = serializerByType[type.Name];
        }
        return serializer;
    }

    public static Action<object, BinaryReader> GetOrCreateDeserializer(Type type)
    {
        Action<object, BinaryReader> deserializer;
        if (!deserializerByType.TryGetValue(type.Name, out deserializer))
        {
            GenerateSerializers(type);
            deserializer = deserializerByType[type.Name];
        }
        return deserializer;
    }
}
