using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// A component which spawns objects, usually in the editor. Calls OnObjectUpdate when an object is spawned or changed. Inheritable.
/// </summary>
public class ObjectSpawner : MonoBehaviour {
    [Tooltip("The type of object to spawn")]
    public GameObject objectType;

    [Range(0.1f, 10)]
    [Tooltip("Distance between each object")]
    public float objectSpacing = 1;

    [Tooltip("Maximum number of objects to spawn")]
    public int maxNumObjects = 10;

    /// <summary>
    /// Turns invisible in-game
    /// </summary>
    protected virtual void Start () {
        // Disable self in-game
        if (Application.isPlaying && GetComponent<MeshRenderer>())
        {
            GetComponent<MeshRenderer>().enabled = false;
            enabled = false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Refreshes this spawner's child objects
    /// </summary>
    protected virtual void Update () {
        if (!Application.isPlaying)
        {
            RefreshChildren();
        }
	}
#endif

    /// <summary>
    /// Refreshes spawned objects; spawning needed objects and despawning excess objects
    /// </summary>
    public void RefreshChildren()
    {
        if (objectSpacing == 0)
        {
            objectSpacing = 1; // hack: prevent division by 0
        }

#if UNITY_EDITOR
        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

        if (prefabStage)
        {
            // Don't spawn objects in prefab mode because Unity won't allow us to remove them...
            return;
        }
#endif

        if (objectType)
        {
            // Place objects
            for (int i = 0; i < Mathf.Min(GetNumObjects(), maxNumObjects); i++)
            {
                GameObject currentObject;

                // Get or create this object
                if (i < transform.childCount)
                {
                    currentObject = transform.GetChild(i).gameObject;
                }
                else
                {
                    currentObject = Instantiate(objectType);
                    currentObject.transform.SetParent(transform);

                    // Relink it to its prefab
#if UNITY_EDITOR
#pragma warning disable 618, 612
                    currentObject = PrefabUtility.ConnectGameObjectToPrefab(currentObject, objectType);
#pragma warning restore 618, 612
#endif
                }

                OnObjectUpdate(currentObject, i);
            }
        }

        // Remove excess objects
        for (int i = transform.childCount - 1; i >= Mathf.Min(GetNumObjects(), maxNumObjects); i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Overridable. Called when an object's position needs updating
    /// </summary>
    public virtual void OnObjectUpdate(GameObject obj, int objIndex)
    {
        return;
    }

    public virtual int GetNumObjects()
    {
        return 0;
    }
}
