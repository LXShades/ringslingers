﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using MLAPI.Serialization.Pooled;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// A Frame contains a virtual state of the game. It can be Ticked, serialized and deserialized (rewinded).
/// 
/// A caveat is that a Tick will run in the real game rather than inside the frame. This means the frame won't always be synced with the live game state.
/// Deserializing syncs the frame to the game state while serializing syncs the game state to the frame.
/// </summary>
[System.Serializable] // for debugging
public class World : MonoBehaviour
{
    /// <summary>
    /// Time to tick between each physics simulation
    /// </summary>
    public const float physicsFixedDeltaTime = 0.04f;

    /// <summary>
    /// Maximum number of missed physics sims to catch up on before the remainder are discarded
    /// </summary>
    public const int maxPhysicsSimsPerTick = 4;

    /// <summary>
    /// Last time that a physics sim occurred
    /// </summary>
    private float lastPhysicsSimTime;

    /// <summary>
    /// The current state of the game (gameObjects) as simulated. This may switch between server and simulation during ticks
    /// </summary>
    public static World live { get; private set; }

    /// <summary>
    /// The current real state of the game according to the server
    /// </summary>
    public static World server
    {
        get
        {
            if (_server == null)
                _server = new GameObject("WorldServer").AddComponent<World>();

            return _server;
        }
    }
    private static World _server;

    /// <summary>
    /// The current simulated state of the game with ping compensation
    /// </summary>
    public static World simulation
    {
        get
        {
            if (_simulation == null)
                _simulation = new GameObject("WorldSim").AddComponent<World>();

            return _simulation;
        }
    }
    private static World _simulation;

    [Header("Object management")]
    /// <summary>
    /// Complete list of syncedObjects in the active scene
    /// </summary>
    public List<WorldObject> worldObjects = new List<WorldObject>();

    /// <summary>
    /// The accumulated game time at the beginning of this tick
    /// </summary>
    public float time
    {
        get; private set;
    }

    // Game time
    /// <summary>
    /// The delta time of the GameState, valid while ticking, adding up to the game time of the next GameState
    /// </summary>
    public float deltaTime
    {
        get; private set;
    }

    /// <summary>
    /// Whether this is a re-run of past events, valid while ticking. Uses include hiding sounds and console messages during replays of past states
    /// </summary>
    public bool isResimulation
    {
        get; private set;
    }

    /// <summary>
    /// Snapshot of the gamestate before the controls were executed. plz no modify.
    /// </summary>
    public Stream preSnapshot
    {
        get; private set;
    }

    public PhysicsScene physics
    {
        get; private set;
    }

    /// <summary>
    /// Constructs a new empty World
    /// </summary>
    void Awake()
    {
        Scene myScene = SceneManager.CreateScene($"{gameObject.name}_Scene", new CreateSceneParameters() { localPhysicsMode = LocalPhysicsMode.Physics3D });
        physics = myScene.GetPhysicsScene();

        SceneManager.MoveGameObjectToScene(gameObject, myScene);
    }

    /// <summary>
    /// Called early on. Initialises stray WorldObjects and attaches them to this world
    /// </summary>
    public void CaptureSceneObjects()
    {
        World.live = this;

        foreach (WorldObject worldObj in GameObject.FindObjectsOfType<WorldObject>())
        {
            if (worldObj.world == null)
            {
                worldObj._OnCreatedByWorld(this, worldObjects.Count);
                worldObjects.Add(worldObj);

                worldObj.transform.SetParent(transform);
            }
        }
    }

    /// <summary>
    /// Advances the game by the given delta time and returns the new tick representing the resulting game state
    /// This causes GameState.live to be replaced with the new state, and may affect all in-game synced objects
    /// </summary>
    public void Tick(MsgTick tick, bool isResimulation)
    {
        World.live = this;

        // Update timing
        time = tick.time + tick.deltaTime;
        deltaTime = tick.deltaTime;
        this.isResimulation = isResimulation;

        // Spawn players who are pending a join
        for (int p = 0; p < Netplay.singleton.players.Length; p++)
        {
            if (Netplay.singleton.players[p] == null && tick.isPlayerInGame[p])
                Netplay.singleton.AddPlayer(p);
            else if (Netplay.singleton.players[p] != null && !tick.isPlayerInGame[p])
                Netplay.singleton.RemovePlayer(p);
        }

        // Read syncers
        if (tick.syncers.Length > 0)
        {
            tick.syncers.Position = 0;
            while (tick.syncers.Position < tick.syncers.Length)
            {
                int player = tick.syncers.ReadByte();
                Netplay.singleton.players[player].movement.ReadSyncer(tick.syncers);
            }
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
        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] && worldObjects[i].gameObject.activeInHierarchy && worldObjects[i].enabled)
            {
                if (worldObjects[i].creationTime == time)
                    worldObjects[i].FrameStart();

                worldObjects[i].FrameUpdate();
            }
        }

        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] && worldObjects[i].gameObject.activeInHierarchy && worldObjects[i].enabled)
                worldObjects[i].FrameLateUpdate();
        }

        // Simulate physics
        if (Physics.autoSimulation)
        {
            Physics.autoSimulation = false;
            Physics.autoSyncTransforms = true;
        }

        int numPhysicsSimsOccurred = 0;
        for (int i = 1; i <= maxPhysicsSimsPerTick; i++)
        {
            if (time - lastPhysicsSimTime >= physicsFixedDeltaTime * i)
            {
                Physics.Simulate(physicsFixedDeltaTime);
                numPhysicsSimsOccurred++;
            }
        }

        lastPhysicsSimTime = Mathf.Clamp(lastPhysicsSimTime + physicsFixedDeltaTime * numPhysicsSimsOccurred, time - physicsFixedDeltaTime*2, time);
    }

    #region Serialization
    /// <summary>
    /// Serializes the world and stuff
    /// </summary>
    public MemoryStream Serialize()
    {
        MemoryStream stream = new MemoryStream(1024 * 1024);

        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            // temp, probably, or to move. this stuff is needed
            writer.Write(time);
            writer.Write(lastPhysicsSimTime);
            writer.Write(worldObjects.Count);

            for (int id = 0; id < worldObjects.Count; id++)
            {
                WorldObject obj = worldObjects[id];

                if (obj == null || obj.isDead || !obj.isActiveAndEnabled)
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

    /// <summary>
    /// Deserializes the world and magic etc
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public bool Deserialize(Stream stream)
    {
        if (stream == null || stream.Position >= stream.Length)
        {
            Debug.LogWarning("Stream is zero - did you forget to reset position?");
            return false;
        }

        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            // Read in the objects
            int objCount;
            time = reader.ReadSingle();
            lastPhysicsSimTime = reader.ReadSingle();
            objCount = reader.ReadInt32();

            if (objCount < worldObjects.Count)
            {
                // Destroy objects that didn't exist yet I guessssss
                for (int i = objCount; i < worldObjects.Count; i++)
                {
                    if (!worldObjects[i].isDead)
                    {
                        if (worldObjects[i].GetComponent<Player>() != null)
                            continue; // don't delete players now

                        DestroyObject(worldObjects[i].gameObject);
                    }
                }

                worldObjects.RemoveRange(objCount, worldObjects.Count - objCount);
            }

            int lastObjId = -1;
            for (ushort objId = reader.ReadUInt16(); objId != 65535; objId = reader.ReadUInt16())
            {
                int size = reader.ReadInt16();

                if (worldObjects[objId].isDead)
                    RestoreObject(worldObjects[objId].gameObject);
                for (ushort cleanup = (ushort)(lastObjId + 1); cleanup < objId; cleanup++)
                {
                    if (worldObjects[objId] && !worldObjects[objId].isDead)
                        DestroyObject(worldObjects[objId].gameObject);
                }

                if (worldObjects[objId])
                    worldObjects[objId].Deserialize(reader);
                else
                    stream.Position += size;

                lastObjId = objId;
            }
        }

        return true;
    }
    #endregion

    #region SyncedObjects
    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject obj = GameObject.Instantiate(prefab, position, rotation);
        WorldObject worldObj = obj.GetComponent<WorldObject>();

        Debug.Assert(worldObj);
        worldObj._OnCreatedByWorld(this, worldObjects.Count);
        worldObjects.Add(worldObj);
        
        return obj;
    }

    public void DestroyObject(GameObject obj)
    {
        WorldObject worldObj = obj.GetComponent<WorldObject>();

        worldObj._OnDestroyedByWorld(this);
        worldObjects[worldObj.objId] = null;
    }

    public void RestoreObject(GameObject obj)
    {
        WorldObject worldObj = obj.GetComponent<WorldObject>();

        worldObj._OnRestoredByWorld(this);
    }
    #endregion

    public void CloneFrom(World source)
    {
        // todo: replace objects of incorrect types
        // todo: remove objects we have that the original doesn't

        // Spawn new object we don't have
        for (int i = worldObjects.Count; i < source.worldObjects.Count; i++)
        {
            GameObject obj = Instantiate(source.worldObjects[i].gameObject);

            obj.GetComponent<WorldObject>()._OnCreatedByWorld(this, i);
            worldObjects.Add(obj.GetComponent<WorldObject>());

            obj.transform.parent = transform;

            // hack
            Camera cam = obj.GetComponent<Camera>();
            AudioListener listener = obj.GetComponent<AudioListener>();
            if (cam && this != World.server)
                cam.pixelRect = new Rect(Screen.width * 0.75f, Screen.height * 0.75f, Screen.width * 0.25f, Screen.height * 0.25f);
            if (listener && this != World.server)
                Destroy(listener);
        }

        // Copy the object's old state(s)
        for (int i = 0; i < source.worldObjects.Count; i++)
        {
            worldObjects[i].CloneFrom(source.worldObjects[i]);
        }
    }
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