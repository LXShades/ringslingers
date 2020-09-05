using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Mirror;

/// <summary>
/// A world object is a cloneable object that exists in the world and should be synchronised between client and server
/// During typical gameplay, two copies of the object exist (one in each world). The server one is reality while the simulation one is ping-projected reality.
/// The ping-projected objects are regularly reset to their server positions via clone. This may have unexpected implications.
/// For now, just bear this in mind if things break.
/// </summary>
public class WorldObject : NetworkBehaviour
{
    public bool hasStarted { get; private set; } = false;

    /// <summary>
    /// World that this object belongs in
    /// </summary>
    public World world { get; private set; }

    /// <summary>
    /// What time this object was created, in its parent world's time
    /// </summary>
    public float creationTime { get; private set; }

    /// <summary>
    /// Whether this object is flagged as destroyed
    /// </summary>
    public bool isDead
    {
        get; private set;
    }

    [System.NonSerialized]
    public List<WorldObjectComponent> worldObjectComponents = new List<WorldObjectComponent>();

    #region Initialisation
    private void Awake()
    {
        World.live.OnWorldObjectSpawned(this);
    }
    #endregion

    #region Lifetime functions
    public void FrameAwake()
    {
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.WorldAwake();
    }

    public void FrameStart()
    {
        hasStarted = true;

        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.WorldStart();
    }

    public void FrameUpdate(float deltaTime)
    {
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.WorldUpdate(deltaTime);
    }

    public void FrameLateUpdate(float deltaTime)
    {
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.WorldLateUpdate(deltaTime);
    }
    #endregion

    #region Management
    /// <summary>
    /// Initial setup on creation
    /// </summary>
    public void _OnCreatedByWorld(World parent, int id)
    {
        world = parent;
        creationTime = parent.gameTime;
        hasStarted = false;

        foreach (WorldObjectComponent objComponent in GetComponentsInChildren<WorldObjectComponent>())
            worldObjectComponents.Add(objComponent);

        if (transform.parent && transform.parent.GetComponentInParent<WorldObject>() != null)
            Debug.LogError("Multiple WorldObjects found in parent chain - this is not allowed. The WorldObject must be at the top of the prefab heirarchy.");
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<WorldObject>())
                Debug.LogError("WorldObjects found in child of world object - this should not happen, it's confusing af and breaks things! One WorldObject must be at the top of its heirarchy.");
        }

        // Call Awake proper real-like
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
        {
            try
            {
                objComponent.WorldAwake();
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception on WorldAwake: {e.Message}");
            }
        }

        // Spawn the object on clients?
        if (GetComponent<Mirror.NetworkIdentity>() && Mirror.NetworkServer.active)
        {
            Mirror.NetworkServer.Spawn(gameObject);
        }
    }

    public void _OnDestroyedByWorld(World parent)
    {
        Debug.Assert(parent == world);

        /*foreach (Collider collider in obj.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false; // collisions can occur with destroyed objects! during resimulation
        }*/

        isDead = true;
        gameObject.SetActive(false);
    }

    public void _OnRestoredByWorld(World parent)
    {
        Debug.Assert(parent == world);

        isDead = false;
        gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        world.OnWorldObjectDestroyed(this);
    }

    public void FlagAsRestored()
    {
        isDead = false;
    }
    #endregion

    #region Serialization
    public void Serialize(BinaryWriter stream)
    {
        stream.Write(creationTime);

        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.Serialize(stream);
    }

    public void Deserialize(BinaryReader stream)
    {
        creationTime = stream.ReadSingle();

        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.Deserialize(stream);
    }
    #endregion

    #region Cloning
    public void CloneFrom(WorldObject source)
    {
        Debug.Assert(source.worldObjectComponents.Count == worldObjectComponents.Count);

        for (int i = 0; i < worldObjectComponents.Count; i++)
        {
            Debug.Assert(worldObjectComponents[i].GetType() == source.worldObjectComponents[i].GetType());
            worldObjectComponents[i].CloneFrom(source.worldObjectComponents[i]);
        }
    }
    #endregion
}
