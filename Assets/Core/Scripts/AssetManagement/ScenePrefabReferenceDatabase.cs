using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Im very sad that this needs to exist.
/// 
/// Unity scenes don't save the refernces to prefabs that at instantiated in the scenes, instead it unpacks them.
/// 
/// This means that if a user makes a mod with a map and it has rings from version 1 of the game, but version 3 of the game introduces a different prefab structure for rings, we're in trouble.
/// 
/// To aid forward-compatibility, we'll manually record all prefab instances in this scene when we save it.
/// </summary>
public class ScenePrefabReferenceDatabase : MonoBehaviour
{
    [System.Serializable]
    public struct ScenePrefabInstance
    {
        public GameObject Object;
        public int PrefabId;
    }

    [Header("Auto-Generated for forward-compatibility support. Please don't modify these.")]
    public ScenePrefabInstance[] prefabInstances;
    public string[] prefabGuidsById;
    public GameObject[] prefabReferencesById; // maybe this will work without duplicating them...?

    private void Awake()
    {
        // When needed, we'll write code to re-instance prefabs that need upgrading in old mod scenes.
        // woo tech debt


        //TestReinstancePrefabs();
    }

    /// <summary>
    /// A test function for re-instancing prefabs
    /// 
    /// This will be needed for backwards compatibility if an old scene has an old version of a prefab with outdated changes
    /// </summary>
    private void TestReinstancePrefabs()
    {
        if (prefabReferencesById == null)
            return;

        // try and re-instantiate prefab references containing "Rail"
        foreach (var instance in prefabInstances)
        {
            if (prefabReferencesById[instance.PrefabId] != null && prefabReferencesById[instance.PrefabId].name.Contains("Rail"))
            {
                GameObject instantiatedClone = Instantiate(prefabReferencesById[instance.PrefabId], instance.Object.transform.position + Vector3.up, Quaternion.identity);
                if (instantiatedClone.TryGetComponent(out NetworkIdentity netIdentity) && instance.Object.TryGetComponent(out NetworkIdentity originalNetIdentity))
                    netIdentity.sceneId = originalNetIdentity.sceneId;
                Destroy(instance.Object);
            }
        }
    }
}
