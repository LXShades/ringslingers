using Mirror;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
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

    private float lastProcessedServerTickTime;

    /// <summary>
    /// Constructs a new empty World
    /// </summary>
    void Start()
    {
        physics = SceneManager.GetActiveScene().GetPhysicsScene();
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
        return Instantiate(prefab, position, rotation);
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
    #endregion
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 0)]
public struct PlayerInput
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
