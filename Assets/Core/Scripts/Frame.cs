using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using System;
using MLAPI.Serialization.Pooled;

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
        //syncedObjects.RemoveAll(a => a == null);
    }

    #region Serialization
    public MemoryStream Serialize()
    {
        MemoryStream stream = new MemoryStream(1024 * 1024);

        for (int id = 0; id < Netplay.singleton.syncedObjects.Count; id++)
        {
            SyncedObject obj = Netplay.singleton.syncedObjects[id];

            if (obj == null)
                continue;

            stream.WriteByte((byte)(id & 255));
            stream.WriteByte((byte)((id >> 8) & 255));
            long sizePos = stream.Position;
            stream.WriteByte(0);
            stream.WriteByte(0);

            obj.Serialize(stream);

            long endPos = stream.Position;
            stream.Position = sizePos;
            stream.WriteByte((byte)((endPos - sizePos - 2) & 255));
            stream.WriteByte((byte)(((endPos - sizePos - 2) >> 8) & 255));
            stream.Position = endPos;
        }
        stream.WriteByte(255);
        stream.WriteByte(255);

        return stream;
    }

    public bool Deserialize(Stream stream)
    {
        if (stream.Length <= stream.Position)
            return false;

        for (int objId = stream.ReadByte() | (stream.ReadByte() << 8); objId != -1 && objId != 65535; objId = stream.ReadByte() | (stream.ReadByte() << 8))
        {
            int size = stream.ReadByte() | (stream.ReadByte() << 8);

            if (Netplay.singleton.syncedObjects[objId])
            {
                Netplay.singleton.syncedObjects[objId].Deserialize(stream);
            }
            else
                stream.Position += size;
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
        using (PooledBitStream data = PooledBitStream.Get())
        {
            using (PooledBitWriter stream = PooledBitWriter.Get(data))
            {
                stream.WriteSinglePacked(moveHorizontalAxis);
                stream.WriteSinglePacked(moveVerticalAxis);
                stream.WriteSinglePacked(horizontalAim);
                stream.WriteSinglePacked(verticalAim);

                stream.WriteBit(btnJump);
                stream.WriteBit(btnFire);
                stream.WritePadBits();
            }

            data.CopyTo(output);
        }
    }

    public void FromStream(Stream input)
    {
        using (PooledBitReader stream = PooledBitReader.Get(input))
        {
            moveHorizontalAxis = stream.ReadSinglePacked();
            moveVerticalAxis = stream.ReadSinglePacked();
            horizontalAim = stream.ReadSinglePacked();
            verticalAim = stream.ReadSinglePacked();

            btnJump = stream.ReadBit();
            btnFire = stream.ReadBit();
            stream.SkipPadBits();
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