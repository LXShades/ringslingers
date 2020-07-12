using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// A world object is a cloneable object that exists in the world and should be synchronised between client and server
/// During typical gameplay, two copies of the object exist (one in each world). The server one is reality while the simulation one is ping-projected reality.
/// The ping-projected objects are regularly reset to their server positions via clone. This may have unexpected implications.
/// For now, just bear this in mind if things break.
/// </summary>
public class WorldObject : MonoBehaviour
{
    private int _id = -1;
    private World _world = null;

    public bool hasStarted { get; private set; } = false;

    /// <summary>
    /// Locally owned unique ID for this component
    /// </summary>
    public int objId => _id;

    /// <summary>
    /// World that this object belongs in
    /// </summary>
    public World world => _world;

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

    private List<WorldObjectComponent> worldObjectComponents = new List<WorldObjectComponent>();

    #region Initialisation
    private void Start()
    {
        if (_world == null)
            Debug.LogError($"WorldObject {gameObject.name} was not instantiated via GameManager.SpawnObject or World.live.SpawnObject. Errors may occur.");
    }
    #endregion

    #region Lifetime functions
    public void FrameAwake()
    {
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.FrameAwake();
    }

    public void FrameStart()
    {
        hasStarted = true;

        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.FrameStart();
    }

    public void FrameUpdate()
    {
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.FrameUpdate();
    }

    public void FrameLateUpdate()
    {
        foreach (WorldObjectComponent objComponent in worldObjectComponents)
            objComponent.FrameLateUpdate();
    }
    #endregion

    #region Management
    /// <summary>
    /// Initial setup on creation
    /// </summary>
    public void _OnCreatedByWorld(World parent, int id)
    {
        _id = id;
        _world = parent;
        creationTime = parent.time;
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
            objComponent.FrameAwake();
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
        if (!isDead && GameManager.singleton) // GameManager.singleton .... should indicate whether the game is ending...except when it doesn't
        {
            Debug.LogError("Synced objects should be destroyed with GameManager.DestroyObject");
            Debug.Break();
        }
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
