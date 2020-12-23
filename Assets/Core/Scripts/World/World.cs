using Mirror;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The World is kind of like a, sort of a.
/// What is the "world"?
/// 
/// W is for Wanted to makealockstepengine
/// O is for Original plan,
/// R is for Refactoring, lockstep's out, deltatime's king,
/// L is for Left this code in,
/// D is for Don't use five letters for a 4/4 time song
/// </summary>
[System.Serializable] // for debugging
public class World : MonoBehaviour, ISyncAction<World.SyncActionSpawnObject>
{
    /// <summary>
    /// Local time since the world was created
    /// </summary>
    public float localTime;

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
    /// The current state of the game
    /// </summary>
    public static World live
    {
        get
        {
            if (_live == null)
                _live = new GameObject("World").AddComponent<World>();

            return _live;
        }
    }
    private static World _live;

    [Header("World objects")]
    /// <summary>
    /// Complete list of syncedObjects in the active scene
    /// </summary>
    public List<WorldObject> worldObjects = new List<WorldObject>();

    /// <summary>
    /// The accumulated game time at the beginning of this tick
    /// </summary>
    public float gameTime
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

    public PhysicsScene physics
    {
        get; private set;
    }

    public struct SyncActionSpawnObject : NetworkMessage
    {
        public GameObject prefab
        {
            set
            {
                if (value)
                {
                    NetworkIdentity identity = value.GetComponent<NetworkIdentity>();

                    if (identity)
                    {
                        assetId = value.GetComponent<NetworkIdentity>().assetId;
                    }

                    if (identity == null || assetId == Guid.Empty)
                    {
                        Log.WriteWarning($"Could not obtain an assetId from object \"{value.name}\"");
                        assetId = Guid.Empty;
                    }
                }
            }
            get
            {
                if (assetId != Guid.Empty && Netplay.singleton.networkedPrefabs.TryGetValue(assetId, out GameObject prefabObject))
                {
                    return prefabObject;
                }
                else
                {
                    Log.WriteWarning($"Could not find prefab of ID {assetId}");
                    return null;
                }
            }
        }

        public Guid assetId;
        public Vector3 position;
        public Quaternion rotation;
        public WorldObject spawnedObject;
        public uint networkedObjectId;
    }

    /// <summary>
    /// Constructs a new empty World
    /// </summary>
    void Start()
    {
        physics = SceneManager.GetActiveScene().GetPhysicsScene();

        SyncActionSystem.RegisterSyncActions(gameObject, true);
    }

    /// <summary>
    /// Advances the game by the given delta time and returns the new tick representing the resulting game state
    /// This causes GameState.live to be replaced with the new state, and may affect all in-game synced objects
    /// </summary>
    public void Tick(float deltaTime)
    {
        // Update timing
        localTime += Time.deltaTime;
        gameTime += Time.deltaTime;
        this.deltaTime = deltaTime;

        // Update all game objects!
        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] && worldObjects[i].gameObject.activeInHierarchy && worldObjects[i].enabled)
            {
                if (!worldObjects[i].hasStarted)
                    worldObjects[i].FrameStart();

                worldObjects[i].FrameUpdate(deltaTime);
            }
        }

        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] && worldObjects[i].gameObject.activeInHierarchy && worldObjects[i].enabled)
                worldObjects[i].FrameLateUpdate(deltaTime);
        }

        // Simulate physics!
        if (Physics.autoSimulation)
        {
            Physics.autoSimulation = false;
            Physics.autoSyncTransforms = true;
        }

        int numPhysicsSimsOccurred = 0;
        for (int i = 1; i <= maxPhysicsSimsPerTick; i++)
        {
            if (gameTime - lastPhysicsSimTime >= physicsFixedDeltaTime * i)
            {
                Physics.SyncTransforms();
                physics.Simulate(physicsFixedDeltaTime);
                numPhysicsSimsOccurred++;
            }
        }

        lastPhysicsSimTime = Mathf.Clamp(lastPhysicsSimTime + physicsFixedDeltaTime * numPhysicsSimsOccurred, gameTime - physicsFixedDeltaTime*2, gameTime);
    }

    #region Spawn/despawn
    public static GameObject Spawn(GameObject prefab)
    {
        return Spawn(prefab, Vector3.zero, Quaternion.identity);
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject spawnedObject = Instantiate(prefab, position, rotation);

        if (NetworkServer.active)
        {
            NetworkServer.Spawn(spawnedObject);
        }

        return spawnedObject;
    }

    public static void Despawn(GameObject target)
    {
        Destroy(target);
    }

    public void OnWorldObjectSpawned(WorldObject obj)
    {
        obj._OnCreatedByWorld(this, worldObjects.Count);
        worldObjects.Add(obj);
    }

    public void OnWorldObjectDestroyed(WorldObject obj)
    {
        int index = worldObjects.IndexOf(obj);

        if (index >= 0)
        {
            worldObjects[index] = null;
        }
    }

    public bool OnPredict(SyncActionChain chain, ref SyncActionSpawnObject data)
    {
        Log.Write("Predicting (then confirming) spawn");
        return OnConfirm(chain, ref data);
    }

    public void OnRewind(SyncActionChain chain, ref SyncActionSpawnObject data)
    {
        if (data.spawnedObject)
        {
            Despawn(data.spawnedObject.gameObject);
            data.spawnedObject = null;
        }

        if (data.networkedObjectId != uint.MaxValue)
        {
            NetworkIdentity.spawned.TryGetValue(data.networkedObjectId, out NetworkIdentity networkedObject);

            if (networkedObject != null)
            {
                data.spawnedObject = networkedObject.GetComponent<WorldObject>();
            }

            if (data.spawnedObject == null)
            {
                Log.WriteError($"Could not find a networked object confirmed by the server of ID {data.networkedObjectId}");
            }
        }

        return;
    }

    public bool OnConfirm(SyncActionChain chain, ref SyncActionSpawnObject data)
    {
        if (Netplay.singleton.networkedPrefabs.TryGetValue(data.assetId, out GameObject prefab))
        {
            data.spawnedObject = Spawn(prefab, data.position, data.rotation).GetComponent<WorldObject>();

            if (NetworkServer.active)
            {
                if (data.spawnedObject && data.spawnedObject.GetComponent<NetworkIdentity>())
                {
                    data.networkedObjectId = data.spawnedObject.GetComponent<NetworkIdentity>().netId;
                }
                else
                {
                    data.networkedObjectId = uint.MaxValue; // spawned object isn't a valid networked object
                    Log.WriteWarning($"Spawned object {data.spawnedObject} isn't networked but was attempted to be linked up via a SpawnObject SyncAction. Should this ever happen? It might be fine for special effects but I dunno");
                }
            }

            return true;
        }
        else
        {
            Log.WriteError($"Spawn failed: Prefab not found from Asset ID {data.assetId}");
            data.spawnedObject = null;
            data.networkedObjectId = uint.MaxValue;
            return false;
        }
    }
    #endregion

    public WorldObject FindWorldObjectById(int localId)
    {
        int index = (localId & 0xFFFFFF);
        if (index < worldObjects.Count)
        {
            if (worldObjects[index] != null && (worldObjects[index].localId >> 26) == Netplay.singleton.localPlayerId)
            {
                return worldObjects[index];
            }
        }

        return null;
    }
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 0)]
public struct PlayerInput : IEquatable<PlayerInput>
{
    public float moveHorizontalAxis;
    public float moveVerticalAxis;

    public float horizontalAim;
    public float verticalAim;

    public bool btnJump;
    public bool btnFire;

    public Vector3 aimDirection
    {
        get
        {
            float horizontalRads = horizontalAim * Mathf.Deg2Rad, verticalRads = verticalAim * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(horizontalRads) * Mathf.Cos(verticalRads), -Mathf.Sin(verticalRads), Mathf.Cos(horizontalRads) * Mathf.Cos(verticalRads));
        }
    }

    /// <summary>
    /// Generates input commands from the current input
    /// </summary>
    /// <param name="lastInput"></param>
    /// <returns></returns>
    public static PlayerInput MakeLocalInput(PlayerInput lastInput)
    {
        PlayerInput localInput;

        localInput.moveHorizontalAxis = Input.GetAxisRaw("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxisRaw("Vertical");

        localInput.horizontalAim = (lastInput.horizontalAim + Input.GetAxis("Mouse X") % 360 + 360) % 360;
        localInput.verticalAim = Mathf.Clamp(lastInput.verticalAim - Input.GetAxis("Mouse Y"), -89.99f, 89.99f);

        localInput.btnFire = Input.GetButton("Fire");
        localInput.btnJump = Input.GetButton("Jump");

        return localInput;
    }

    public void Deserialize(NetworkReader reader)
    {
        moveHorizontalAxis = reader.ReadSingle();
        moveVerticalAxis = reader.ReadSingle();
        horizontalAim = reader.ReadSingle();
        verticalAim = reader.ReadSingle();

        byte buttons = reader.ReadByte();
        btnJump = (buttons & 1) != 0;
        btnFire = (buttons & 2) != 0;
    }

    public bool Equals(PlayerInput other)
    {
        return moveHorizontalAxis == other.moveHorizontalAxis && moveVerticalAxis == other.moveVerticalAxis
            && horizontalAim == other.horizontalAim && verticalAim == other.verticalAim
            && btnFire == other.btnFire && btnJump == other.btnJump;
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.WriteSingle(moveHorizontalAxis);
        writer.WriteSingle(moveVerticalAxis);
        writer.WriteSingle(horizontalAim);
        writer.WriteSingle(verticalAim);
        writer.WriteByte((byte)((btnJump ? 1 : 0) | (btnFire ? 2 : 0)));
    }
}

public static class PlayerInputSerializer
{
    public static void WritePlayerInput(this NetworkWriter writer, PlayerInput input)
    {
        input.Serialize(writer);
    }

    public static PlayerInput ReadPlayerInput(this NetworkReader reader)
    {
        PlayerInput output = new PlayerInput();

        output.Deserialize(reader);

        return output;
    }
}
