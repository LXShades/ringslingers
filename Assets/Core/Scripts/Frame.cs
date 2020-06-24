using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI.Messaging;
using System.Runtime.InteropServices;
using System.IO;
using System;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// A Frame contains the current virtual state of the game. It can be Advanced, serialized and deserialized (rewinded).
/// 
/// Serializing and Deserializing a frame works in tandem with the state of objects in the Unity game engine.
/// In other words Deserializing a frame will update object positions and variables too
/// This may change in the future as it is a little bit finnicky- for example we can't 'peek' at a frame's objects without loading them into the real scene.
/// It would be nice if we could store them in the frame though. We'll see where it goes.
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
                _current = GameManager.singleton.localFrame;
            }

            return _current;
        }
    }
    private static Frame _current;

    /// <summary>
    /// The actual physical game frame
    /// </summary>
    public static Frame server
    {
        get
        {
            if (_server == null)
            {
                _server = GameManager.singleton.serverFrame;
            }

            return _server;
        }
    }
    private static Frame _server;

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
    /// Fixed delta time running at the server tick rate
    /// </summary>
    public const float tickDeltaTime = 0.1f;

    /// <summary>
    /// Player inputs by ID at the beginning of this frame
    /// </summary>
    public InputCmds[] playerInputs = new InputCmds[GameManager.maxPlayers];

    /// <summary>
    /// Players by ID. May contain null gaps
    /// </summary>
    public Player[] players = new Player[GameManager.maxPlayers];

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
        for (int i = 0; i < GameManager.maxPlayers; i++)
        {
            if (players[i])
            {
                players[i].lastInput = players[i].input;
                players[i].input = playerInputs[i];
            }
        }

        // Update game objects
        foreach (SyncedObject obj in GameManager.singleton.syncedObjects)
        {
            obj.TriggerStartIfCreated();
            obj.FrameUpdate();
        }

        foreach (SyncedObject obj in GameManager.singleton.syncedObjects)
        {
            obj.FrameLateUpdate();
        }
    }


    /// <summary>
    /// Serializes the current player inputs into a stream
    /// </summary>
    public System.IO.Stream ReadInputs()
    {
        System.IO.MemoryStream output = new System.IO.MemoryStream(2000);

        for (int i = 0; i < GameManager.maxPlayers; i++)
        {
            if (players[i])
            {
                output.WriteByte((byte)i);
                players[i].input.ToStream(output);
            }
        }

        return output;
    }

    public Player CmdAddPlayer()
    {
        // Find the appropriate ID for this player
        int freeId = -1;
        for (freeId = 0; freeId < GameManager.maxPlayers; freeId++)
        {
            if (players[freeId] == null)
            {
                break;
            }
        }

        if (freeId == GameManager.maxPlayers)
        {
            Debug.LogWarning("Can't add new player - too many!");
            return null;
        }

        Player player = GameObject.Instantiate(GameManager.singleton.playerPrefab).GetComponent<Player>();

        player.playerId = freeId;
        players[freeId] = player;

        return player;
    }


    #region Serialization
    public MemoryStream Serialize()
    {
        MemoryStream stream = new MemoryStream(1024 * 1024);
        byte[] byteBuffer = new byte[32];

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
        return stream;
    }

    public bool Deserialize(Stream stream)
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
    #endregion
}

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
}