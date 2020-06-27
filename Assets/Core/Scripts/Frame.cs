using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using System;

/// <summary>
/// A Frame contains a virtual state of the game. It can be Ticked, serialized and deserialized (rewinded).
/// 
/// A caveat is that a Tick will run in the real game rather than inside the frame. This means the frame won't always be synced with the live game state.
/// Deserializing syncs the frame to the game state while serializing syncs the game state to the frame.
/// </summary>
public class Frame
{
    /// <summary>
    /// The current game frame according to current simulations
    /// </summary>
    public static Frame local
    {
        get
        {
            if (_current == null)
            {
                _current = new Frame();
            }

            return _current;
        }
    }
    private static Frame _current;

    // Game time
    /// <summary>
    /// The delta time of the current tick
    /// </summary>
    public float deltaTime;

    /// <summary>
    /// The time at the current tick
    /// </summary>
    public float time;

    /// <summary>
    /// Player inputs by ID at the beginning of this frame
    /// </summary>
    public InputCmds[] playerInputs = new InputCmds[Netplay.maxPlayers];

    /// <summary>
    /// Advances the game by the given delta time
    /// </summary>
    /// <param name="deltaTime"></param>
    public void Tick(float deltaTime)
    {
        // Update timing
        this.deltaTime = deltaTime;
        time = time + deltaTime;

        // Apply player inputs
        for (int i = 0; i < Netplay.maxPlayers; i++)
        {
            if (Netplay.singleton.players[i])
            {
                Netplay.singleton.players[i].lastInput = Netplay.singleton.players[i].input;
                Netplay.singleton.players[i].input = playerInputs[i];
            }
        }

        // Update game objects
        List<SyncedObject> syncedObjects = Netplay.singleton.syncedObjects;
        for (int i = 0; i < syncedObjects.Count; i++)
        {
            if (syncedObjects[i])
            {
                syncedObjects[i].TriggerStartIfCreated();
                syncedObjects[i].FrameUpdate();
            }
        }

        for (int i = 0; i < syncedObjects.Count; i++)
        {
            if (syncedObjects[i])
            {
                syncedObjects[i].FrameLateUpdate();
            }
        }

        // Cleanup missing synced objects
        syncedObjects.RemoveAll(a => a == null);
    }

    #region Serialization
    public MemoryStream Serialize()
    {
        MemoryStream stream = new MemoryStream(1024 * 1024);
        byte[] byteBuffer = new byte[32];

        foreach (SyncedObject obj in Netplay.singleton.syncedObjects)
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
        return stream;
    }

    public bool Deserialize(Stream stream)
    {
        if (stream.Length <= 0)
            return false;

        byte[] byteBuffer = new byte[32];

        stream.Seek(0, SeekOrigin.Begin);

        foreach (SyncedObject obj in Netplay.singleton.syncedObjects)
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
    #endregion
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 0)]
public struct InputCmds
{
    public float moveHorizontalAxis;
    public float moveVerticalAxis;

    public float horizontalAim;
    public float verticalAim;

    public bool btnJump;
    public bool btnFire;

    public void ToStream(Stream output)
    {
        unsafe
        {
            byte[] streamBytes = new byte[sizeof(InputCmds)];
            fixed (byte* b = streamBytes)
            {
                *(InputCmds*)b = this;
            }

            output.Write(streamBytes, 0, sizeof(InputCmds));
        }
    }

    public void FromStream(Stream input)
    {
        unsafe
        {
            byte[] source = new byte[sizeof(InputCmds)];

            input.Read(source, 0, source.Length);

            fixed (byte* b = source)
            {
                this = *(InputCmds*)b;
            }
        }
    }

    /// <summary>
    /// Generates input commands from the current input
    /// </summary>
    /// <param name="lastInput"></param>
    /// <returns></returns>
    public static InputCmds FromLocalInput(InputCmds lastInput)
    {
        InputCmds localInput;

        localInput.moveHorizontalAxis = Input.GetAxisRaw("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxisRaw("Vertical");

        localInput.horizontalAim = (lastInput.horizontalAim + Input.GetAxis("Mouse X") % 360 + 360) % 360;
        localInput.verticalAim = Mathf.Clamp(lastInput.verticalAim - Input.GetAxis("Mouse Y"), -89.99f, 89.99f);

        localInput.btnFire = Input.GetButton("Fire");
        localInput.btnJump = Input.GetButton("Jump");

        return localInput;
    }
}