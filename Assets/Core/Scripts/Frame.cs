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
    /// The game frame running in the current tick
    /// </summary>
    public static Frame current
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

    /// <summary>
    /// The time at the current tick
    /// </summary>
    public float time;

    // Game time
    /// <summary>
    /// The delta time of the current tick
    /// </summary>
    public float deltaTime;

    /// <summary>
    /// Time to tick between each physics simulation
    /// </summary>
    public float physicsFixedDeltaTime = 0.04f;

    /// <summary>
    /// Maximum number of missed physics sims to catch up on before the remainder are discarded
    /// </summary>
    public int maxAccumulatedPhysicsSims = 4;

    public float lastPhysicsSimTime;

    /// <summary>
    /// Whether the current tick is a re-run of past events, often used for sound culling
    /// </summary>
    public bool isResimulation = false;

    /// <summary>
    /// Advances the game by the given delta time
    /// </summary>
    /// <param name="deltaTime"></param>
    public void Tick(MsgTick tick)
    {
        // Update timing
        deltaTime = tick.deltaTime;

        // Spawn players who are pending a join
        for (int p = 0; p < Netplay.singleton.players.Length; p++)
        {
            if (Netplay.singleton.players[p] == null && tick.isPlayerInGame[p])
                Netplay.singleton.AddPlayer(p);
            else if (Netplay.singleton.players[p] != null && !tick.isPlayerInGame[p])
                Netplay.singleton.RemovePlayer(p);
        }

        // Apply player inputs
        for (int i = 0; i < Netplay.maxPlayers; i++)
        {
            if (Netplay.singleton.players[i])
            {
                Netplay.singleton.players[i].lastInput = Netplay.singleton.players[i].input;
                Netplay.singleton.players[i].input = tick.playerInputs[i];
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
                syncedObjects[i].FrameLateUpdate();
        }

        // Simulate physics
        if (Physics.autoSimulation)
        {
            Physics.autoSimulation = false;
            Physics.autoSyncTransforms = true;
        }

        for (int i = 0; i < maxAccumulatedPhysicsSims; i++)
        {
            if (time - lastPhysicsSimTime >= physicsFixedDeltaTime)
            {
                //Debug.Log($"PHYSX@{time.ToString("#.00")} last {lastPhysicsSimTime.ToString("#.00")}");
                Physics.Simulate(physicsFixedDeltaTime);
                lastPhysicsSimTime += physicsFixedDeltaTime;
            }
        }

        lastPhysicsSimTime = Mathf.Clamp(lastPhysicsSimTime, time - physicsFixedDeltaTime, time);
        time = tick.time + deltaTime;
    }

    #region Serialization
    public MemoryStream Serialize()
    {
        MemoryStream stream = new MemoryStream(1024 * 1024);

        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            // temp, probably, or to move. this stuff is needed
            writer.Write(time);
            writer.Write(lastPhysicsSimTime);
            writer.Write(SyncedObject.GetNextId());

            for (int id = 0; id < Netplay.singleton.syncedObjects.Count; id++)
            {
                SyncedObject obj = Netplay.singleton.syncedObjects[id];

                if (obj == null || obj.isDead)
                    continue;

                writer.Write((ushort)id);
                long sizePos = stream.Position;
                writer.Write((ushort)0);

                obj.Serialize(writer);

                long endPos = stream.Position;
                stream.Position = sizePos;
                writer.Write((ushort)(endPos - sizePos - 2));
                stream.Position = stream.Length;
            }

            writer.Write((ushort)65535);
        }

        return stream;
    }

    public bool Deserialize(Stream stream)
    {
        if (stream.Length <= stream.Position)
        {
            Debug.LogWarning("Stream has ended before Frame.Deserialize - forgot to reset position?");
            return false;
        }

        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            // Read in the objects
            int oldNextId = SyncedObject.GetNextId();

            time = reader.ReadSingle();
            lastPhysicsSimTime = reader.ReadSingle();
            SyncedObject.RevertNextId(reader.ReadInt32());

            int lastObjId = -1;
            for (ushort objId = reader.ReadUInt16(); objId != 65535; objId = reader.ReadUInt16())
            {
                int size = reader.ReadInt16();

                if (Netplay.singleton.syncedObjects[objId].isDead)
                    GameManager.RestoreObject(Netplay.singleton.syncedObjects[objId].gameObject);
                for (ushort cleanup = (ushort)(lastObjId + 1); cleanup < objId; cleanup++)
                {
                    if (Netplay.singleton.syncedObjects[objId] && !Netplay.singleton.syncedObjects[objId].isDead)
                        GameManager.DestroyObject(Netplay.singleton.syncedObjects[objId].gameObject);
                }

                if (Netplay.singleton.syncedObjects[objId])
                    Netplay.singleton.syncedObjects[objId].Deserialize(reader);
                else
                    stream.Position += size;

                lastObjId = objId;
            }

            // Delete objects that didn't exist yet (with a cheap deactivate/activate hack)
            for (int i = SyncedObject.GetNextId(); i < oldNextId; i++)
            {
                Debug.Assert(Netplay.singleton.syncedObjects[i]);
                if (!Netplay.singleton.syncedObjects[i].isDead)
                {
                    if (Netplay.singleton.syncedObjects[i].GetComponent<Player>() != null)
                        continue; // don't delete players now

                    GameManager.DestroyObject(Netplay.singleton.syncedObjects[i].gameObject);
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