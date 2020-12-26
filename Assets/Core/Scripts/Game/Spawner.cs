using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour, ISyncAction<Spawner.SyncActionSpawnObject>
{
    public static Spawner singleton => _singleton ?? (_singleton = FindObjectOfType<Spawner>());
    private static Spawner _singleton;

    private readonly Dictionary<ushort, Predictable> predictedSpawns = new Dictionary<ushort, Predictable>();
    private readonly Dictionary<Guid, GameObject> prefabByGuid = new Dictionary<Guid, GameObject>();

    public List<GameObject> spawnablePrefabs = new List<GameObject>();

    private byte nextClientPredictId = 0;

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
                if (assetId != Guid.Empty && Spawner.singleton.prefabByGuid.TryGetValue(assetId, out GameObject prefabObject))
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
        public GameObject spawnedObject;
        public byte clientPredictId;
        public uint serverNetId;
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        foreach (GameObject spawnable in spawnablePrefabs)
        {
            prefabByGuid.Add(spawnable.GetComponent<NetworkIdentity>().assetId, spawnable);
        }

        SyncActionSystem.RegisterSyncActions(gameObject, true);

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
        }

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadMode) => OnSceneLoaded(scene);
    }

    void Start()
    {
        NetMan.singleton.onClientConnect += (NetworkConnection conn) => RegisterSpawnHandlers();
    }

    void RegisterSpawnHandlers()
    {
        ClientScene.ClearSpawners();

        // Register custom spawn handlers
        foreach (var prefab in spawnablePrefabs)
        {
            ClientScene.RegisterSpawnHandler(prefab.GetComponent<NetworkIdentity>().assetId, SpawnHandler, UnspawnHandler, PostSpawnHandler);
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene)
    {
        if (NetworkServer.active)
        {
            foreach (GameObject obj in scene.GetRootGameObjects())
            {
                foreach (Transform child in obj.transform)
                {
                    if (child.GetComponentInParent<NetworkBehaviour>())
                    {
                        SyncActionSystem.RegisterSyncActions(child.gameObject);
                    }
                }
            }
        }

        RegisterSpawnHandlers();
    }

    public static GameObject StartSpawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return Instantiate(prefab, position, rotation);
    }

    public static void FinalizeSpawn(GameObject target)
    {
        if (NetworkServer.active && target.GetComponent<NetworkIdentity>() != null)
        {
            NetworkServer.Spawn(target);
            SyncActionSystem.RegisterSyncActions(target);
        }
    }

    public static GameObject Spawn(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);

        FinalizeSpawn(obj);
        return obj;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject obj = Instantiate(prefab, position, rotation);

        FinalizeSpawn(obj);
        return obj;
    }

    public static void Despawn(GameObject target)
    {
        Destroy(target);
    }

    public static bool CanPredictSpawn(GameObject prefab)
    {
        return prefab.GetComponent<Predictable>() && prefab.GetComponent<NetworkIdentity>();
    }

    private static GameObject PredictSpawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject obj = Instantiate(prefab, position, rotation);

        if (obj && obj.TryGetComponent(out NetworkIdentity identity) && obj.TryGetComponent(out Predictable predictable))
        {
            // player-specific "predictable object ID" which we can use later to replace the object
            identity.predictedId = (ushort)((Netplay.singleton.localPlayerId << 8) | singleton.nextClientPredictId);
            singleton.predictedSpawns[identity.predictedId] = predictable;
            singleton.nextClientPredictId++;
        }
        else
        {
            Log.WriteError($"Cannot predict {prefab}! It must be predictable and networkable.");
        }

        return obj;
    }

    private GameObject SpawnHandler(SpawnMessage spawnMessage)
    {
        if (spawnMessage.assetId != Guid.Empty)
        {
            if (predictedSpawns.TryGetValue(spawnMessage.predictedId, out Predictable predictable) && predictable != null)
            {
                if (predictable.TryGetComponent(out NetworkIdentity identity) && identity.assetId == spawnMessage.assetId)
                {
                    // it's the same object, and we predicted it, let's replace it!
                    Log.Write($"Successfully predicted spawn of {predictable.gameObject}!");
                    return predictable.gameObject;
                }
            }
            Log.Write($"Spawning a {prefabByGuid[spawnMessage.assetId]}");
            return Spawn(prefabByGuid[spawnMessage.assetId], spawnMessage.position, spawnMessage.rotation);
        }
        else
        {
            return Spawn(ClientScene.spawnableObjects[spawnMessage.sceneId].gameObject, spawnMessage.position, spawnMessage.rotation);
        }
    }

    private void UnspawnHandler(GameObject target)
    {
        Destroy(target);
    }

    private void PostSpawnHandler(GameObject target)
    {
        SyncActionSystem.RegisterSyncActions(target);
    }

    public bool OnPredict(SyncActionChain chain, ref SyncActionSpawnObject data)
    {
        if (data.prefab == null)
        {
            Log.WriteError("Predicted prefab was null - object probably isn't in spawnables.");
            return false;
        }

        if (CanPredictSpawn(data.prefab))
        {
            data.spawnedObject = PredictSpawn(data.prefab, data.position, data.rotation);

            if (data.spawnedObject)
            {
                data.clientPredictId = (byte)data.spawnedObject.GetComponent<NetworkIdentity>().predictedId;
            }
        }

        return true;
    }

    public bool OnConfirm(SyncActionChain chain, ref SyncActionSpawnObject data)
    {
        if (NetworkServer.active)
        {
            if (prefabByGuid.TryGetValue(data.assetId, out GameObject prefab))
            {
                data.spawnedObject = StartSpawn(prefab, data.position, data.rotation);

                if (data.spawnedObject)
                {
                    NetworkIdentity identity = data.spawnedObject.GetComponent<NetworkIdentity>();

                    // inform the client that this is the object they predicted so they can replace it without respawning it
                    identity.predictedId = (ushort)((chain.sourcePlayer << 8) | data.clientPredictId);

                    FinalizeSpawn(data.spawnedObject);

                    if (identity)
                    {
                        // the client shall know what this object is
                        data.serverNetId = identity.netId;
                    }
                }
                else
                {
                    data.serverNetId = uint.MaxValue; // spawned object isn't a valid networked object
                    Log.WriteWarning($"Spawned object {data.spawnedObject} isn't networkable and won't be permanently spawned");
                }

                return true;
            }
            else
            {
                Log.WriteError($"Spawn failed: Prefab not found from Asset ID {data.assetId}");
                data.spawnedObject = null;
                data.serverNetId = uint.MaxValue;
                return false;
            }
        }
        else
        {
            if (NetworkIdentity.spawned.TryGetValue(data.serverNetId, out NetworkIdentity identity) && identity != null)
            {
                data.spawnedObject = identity.gameObject;
                return true;
            }
            else
            {
                Log.WriteError($"Could not retrieve spawned object: object (ID: {data.serverNetId}) not found in spawned)");
                return false;
            }
        }
    }

    public void OnRewind(SyncActionChain chain, ref SyncActionSpawnObject data, bool isConfirmed)
    {
        if (data.spawnedObject && !isConfirmed)
        {
            Despawn(data.spawnedObject.gameObject);
            data.spawnedObject = null;
        }
    }
}
