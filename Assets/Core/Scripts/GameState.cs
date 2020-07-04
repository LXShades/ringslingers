using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using MLAPI.Serialization.Pooled;

/// <summary>
/// A Frame contains a virtual state of the game. It can be Ticked, serialized and deserialized (rewinded).
/// 
/// A caveat is that a Tick will run in the real game rather than inside the frame. This means the frame won't always be synced with the live game state.
/// Deserializing syncs the frame to the game state while serializing syncs the game state to the frame.
/// </summary>
[System.Serializable] // for debugging
public class GameState
{
    /// <summary>
    /// Time to tick between each physics simulation
    /// </summary>
    public const float physicsFixedDeltaTime = 0.04f;

    /// <summary>
    /// Maximum number of missed physics sims to catch up on before the remainder are discarded
    /// </summary>
    public const int maxPhysicsSimsPerTick = 4;

    private float lastPhysicsSimTime;

    /// <summary>
    /// The current state of the game (gameObjects) in GameState form
    /// A snapshot is taken as each new GameState is created, reflecting the pre-tick beginning of the new state
    /// Changes that occur outside of ticks may not be correctly represented. Be mindful.
    /// </summary>
    public static GameState live
    {
        get
        {
            if (_live == null)
                _live = new GameState();

            return _live;
        }
    }
    private static GameState _live;

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

    /// <summary>
    /// Constructs a new empty GameState
    /// </summary>
    private GameState()
    {
    }

    /// <summary>
    /// Constructs a new state with a loose copy of another state
    /// </summary>
    /// <param name="previousState"></param>
    private GameState(GameState source)
    {
        time = source.time;
        lastPhysicsSimTime = source.lastPhysicsSimTime;
        preSnapshot = null;
    }

    /// <summary>
    /// Advances the game by the given delta time and returns the new tick representing the resulting game state
    /// This causes GameState.live to be replaced with the new state, and may affect all in-game synced objects
    /// </summary>
    public static GameState Tick(MsgTick tick, bool isResimulation, bool takeSnapshot)
    {
        GameState currentState = _live;

        // Update timing
        currentState.deltaTime = tick.deltaTime;
        currentState.isResimulation = isResimulation;

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

        int numPhysicsSimsOccurred = 0;
        for (int i = 1; i <= maxPhysicsSimsPerTick; i++)
        {
            if (currentState.time + tick.deltaTime - currentState.lastPhysicsSimTime >= physicsFixedDeltaTime * i)
            {
                Physics.Simulate(physicsFixedDeltaTime);
                numPhysicsSimsOccurred++;
            }
        }

        // Transfer information into the next GameState
        GameState nextState = new GameState(currentState);

        nextState.time = /*currentState.time + tick.deltaTime*/tick.time + tick.deltaTime;
        nextState.lastPhysicsSimTime = Mathf.Clamp(currentState.lastPhysicsSimTime + physicsFixedDeltaTime * numPhysicsSimsOccurred, // prevent buffering too many sims
                                                    nextState.time - physicsFixedDeltaTime, nextState.time);
        _live = nextState;
        _live.preSnapshot = SerializeFromWorld();

        return nextState;
    }

    #region Serialization
    /// <summary>
    /// Loads a new state into GameState.live
    /// </summary>
    public static bool LoadState(GameState state)
    {
        if (state.preSnapshot != null)
        {
            _live = state;
            state.preSnapshot.Position = 0;
            DeserializeToWorld();

            return true;
        }
        else
        {
            Debug.LogWarning("Cannot deserialize state with no snapshot");
            return false;
        }
    }

    /// <summary>
    /// Saves a new state from GameState.live
    /// </summary>
    /// <returns></returns>
    public static GameState SaveState()
    {
        GameState newState = new GameState(_live);

        newState.preSnapshot = SerializeFromWorld();

        return newState;
    }

    private static MemoryStream SerializeFromWorld()
    {
        MemoryStream stream = new MemoryStream(1024 * 1024);

        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            // temp, probably, or to move. this stuff is needed
            writer.Write(_live.time);
            writer.Write(_live.lastPhysicsSimTime);
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

    private static bool DeserializeToWorld()
    {
        Stream stream = _live.preSnapshot;

        if (stream == null || stream.Length <= stream.Position)
        {
            Debug.LogWarning("Stream has ended before Frame.Deserialize - forgot to reset position?");
            return false;
        }

        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            // Read in the objects
            int oldNextId = SyncedObject.GetNextId();

            _live.time = reader.ReadSingle();
            _live.lastPhysicsSimTime = reader.ReadSingle();
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