using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System;
using UnityEngine.UI;

public class GameState
{
    MemoryStream stream;

    public void Serialize()
    {
        byte[] byteBuffer = new byte[32];
        stream = new MemoryStream(1024 * 1024);

        foreach (SyncedObject obj in GameManager.singleton.syncedObjects)
        {
            System.Type objType = obj.GetType();
            System.Reflection.FieldInfo[] fields = objType.GetFields();
            int objId = obj.id;

            foreach (var field in fields)
            {
                object val = field.GetValue(obj);
                Type valType = field.FieldType;

                unsafe
                {
                    switch (Type.GetTypeCode(valType))
                    {
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                            fixed (byte* b = byteBuffer)
                            {
                                *((uint*)b) = (uint)val;
                                stream.Write(byteBuffer, 0, 4);
                            }
                            break;
                        case TypeCode.Single:
                            fixed (byte* b = byteBuffer)
                            {
                                *((float*)b) = (float)val;
                                stream.Write(byteBuffer, 0, 4);
                            }
                            break;
                        case TypeCode.Double:
                            fixed (byte* b = byteBuffer)
                            {
                                *((double*)b) = (double)val;
                                stream.Write(byteBuffer, 0, 8);
                            }
                            break;
                        case TypeCode.String:
                            fixed (byte* b = byteBuffer)
                            {
                                byte[] stringBytes = System.Text.ASCIIEncoding.Default.GetBytes(val as string);
                                *(int*)b = stringBytes.Length;
                                stream.Write(byteBuffer, 0, 4);
                                stream.Write(stringBytes, 0, stringBytes.Length);
                                break;
                            }
                        default:
                            // Non-numeric type
                            if (valType == typeof(Vector3))
                            {
                                fixed (byte* b = byteBuffer)
                                {
                                    Vector3 asVec = (Vector3)val;
                                    *((float*)&b[0]) = asVec.x;
                                    *((float*)&b[4]) = asVec.y;
                                    *((float*)&b[8]) = asVec.z;
                                    stream.Write(byteBuffer, 0, sizeof(float) * 3);
                                    Debug.Log($"Wrote {asVec} to {field.Name} as Vector3");
                                }
                            }
                            else
                            {
                                Debug.Log($"Unhandled type: {field.Name}");
                            }
                            break;
                    } // switch
                } // unsafe
            } // foreach (var field in fields)

            // Also write transform
            unsafe
            {
                fixed (byte* b = byteBuffer)
                {
                    *(float*)&b[0] = obj.transform.position.x;
                    *(float*)&b[4] = obj.transform.position.y;
                    *(float*)&b[8] = obj.transform.position.z;
                    stream.Write(byteBuffer, 0, sizeof(float) * 3);
                }
            }
        }

        Debug.Log($"Stream size: {stream.Position}");
    }

    public bool Deserialize()
    {
        if (stream.Length <= 0)
        {
            return false;
        }

        byte[] byteBuffer = new byte[32];

        stream.Seek(0, SeekOrigin.Begin);

        foreach (SyncedObject obj in GameManager.singleton.syncedObjects)
        {
            Type objType = obj.GetType();
            System.Reflection.FieldInfo[] fields = objType.GetFields();
            int objId = obj.id;

            foreach (var field in fields)
            {
                Type valType = field.FieldType;

                unsafe
                {
                    switch (Type.GetTypeCode(valType))
                    {
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                            fixed (byte* b = byteBuffer)
                            {
                                stream.Read(byteBuffer, 0, 4);
                                field.SetValue(obj, *((uint*)b));
                            }
                            break;
                        case TypeCode.Single:
                            fixed (byte* b = byteBuffer)
                            {
                                stream.Read(byteBuffer, 0, 4);
                                field.SetValue(obj, *((float*)b));
                            }
                            break;
                        case TypeCode.Double:
                            fixed (byte* b = byteBuffer)
                            {
                                stream.Read(byteBuffer, 0, 8);
                                field.SetValue(obj, *((double*)b));
                            }
                            break;
                        case TypeCode.String:
                            fixed (byte* b = byteBuffer)
                            {
                                stream.Read(byteBuffer, 0, 4);
                                int length = *(int*)b;
                                byte[] strBytes = new byte[length];
                                stream.Read(strBytes, 0, length);
                                field.SetValue(obj, System.Text.ASCIIEncoding.Default.GetString(strBytes));
                            }
                            break;
                        default:
                            // Non-numeric type
                            if (valType == typeof(Vector3))
                            {
                                fixed (byte* b = byteBuffer)
                                {
                                    Vector3 vec;
                                    stream.Read(byteBuffer, 0, sizeof(float) * 3);
                                    vec.x = *(float*)&b[0];
                                    vec.y = *(float*)&b[4];
                                    vec.z = *(float*)&b[8];
                                }
                            }
                            else
                            {
                                Debug.Log($"Unhandled type: {field.Name}");
                            }
                            break;
                    } // switch
                } // unsafe
            } // foreach (var field in fields)

            // Also write transform
            unsafe
            {
                fixed (byte* b = byteBuffer)
                {
                    stream.Read(byteBuffer, 0, sizeof(float) * 3);
                    Vector3 pos;
                    pos.x = *(float*)&b[0];
                    pos.y = *(float*)&b[4];
                    pos.z = *(float*)&b[8];

                    obj.transform.position = pos;
                }
            }
        }

        return true;
    }
}
