using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public struct SpawnPrediction
    {
        /// <summary>
        /// The prediction ID of the first object spawned. Does not change, gets sent to server so the server can begin from the same ID.
        /// </summary>
        public ushort startId;

        /// <summary>
        /// The current prediction ID. Incremented after every spawn or spawn prediction. NOT SERIALIZED.
        /// </summary>
        public ushort currentId
        {
            get
            {
                if (!_hasAccessedCurrentId)
                {
                    _hasAccessedCurrentId = true;
                    return (_currentId = startId);
                }
                return _currentId;
            }
            set => _currentId = value;
        }
        private bool _hasAccessedCurrentId;
        private ushort _currentId;

        public void Increment()
        {
            currentId = (ushort)((currentId & ~0xFF) | ((currentId + 1) & 0xFF));

            if (NetworkClient.isConnected && !NetworkServer.active)
                Spawner.singleton.nextClientPredictionId++; // needed for producing future client predictions
        }
    }

    public static Spawner singleton => _singleton ?? (_singleton = FindObjectOfType<Spawner>());
    private static Spawner _singleton;

    private readonly Dictionary<ushort, Predictable> predictedSpawns = new Dictionary<ushort, Predictable>();
    private readonly Dictionary<Guid, GameObject> prefabByGuid = new Dictionary<Guid, GameObject>();

    public List<GameObject> spawnablePrefabs = new List<GameObject>();

    private byte localPlayerId;
    private byte nextClientPredictionId = 0;

    void Awake()
    {
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        foreach (GameObject spawnable in spawnablePrefabs)
        {
            if (spawnable == null)
                continue; // it can happen

            prefabByGuid.Add(spawnable.GetComponent<NetworkIdentity>().assetId, spawnable);
        }

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            // todo: why are we doing this???
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
        NetworkClient.ClearSpawners();

        // Register custom spawn handlers
        foreach (var prefab in spawnablePrefabs)
        {
            if (prefab == null)
                continue;

            NetworkClient.RegisterSpawnHandler(prefab.GetComponent<NetworkIdentity>().assetId, SpawnHandler, UnspawnHandler, PostSpawnHandler);
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene)
    {
        RegisterSpawnHandlers();
    }

    /// <summary>
    /// Starts an object spawn sequence, allowing time for SyncVars to be set
    /// </summary>
    public static GameObject StartSpawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return Instantiate(prefab, position, rotation);
    }

    /// <summary>
    /// Starts an object spawn sequence, allowing time for SyncVars to be set
    /// </summary>
    public static GameObject StartSpawn(GameObject prefab, Vector3 position, Quaternion rotation, ref SpawnPrediction prediction)
    {
        if (NetworkClient.isConnected && !NetworkServer.active && (!prefab.GetComponent<NetworkIdentity>() || !prefab.GetComponent<Predictable>()))
        {
            Debug.LogError($"Cannot predict {prefab.name}! It must be predictable and networkable.");
            return null;
        }

        GameObject obj = Instantiate(prefab, position, rotation);

        if (obj.TryGetComponent(out NetworkIdentity identity) && obj.TryGetComponent(out Predictable predictable))
        {
            // player-specific "predictable object ID" which we can use later to replace the object
            singleton.predictedSpawns[prediction.currentId] = predictable;
            identity.predictedId = prediction.currentId;
            predictable.isPrediction = !NetworkServer.active;

            prediction.Increment(); // consume this prediction, go to next
        }

        return obj;
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

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, ref SpawnPrediction prediction)
    {
        GameObject obj = StartSpawn(prefab, position, rotation);

        if (obj)
            FinalizeSpawn(obj);

        return obj;
    }

    /// <summary>
    /// Finalizes and networks a spawn, sending relevant info to clients if server.
    /// Client predictions do nothing extra, but this can be called anyway so the code path can be reused
    /// </summary>
    /// <param name="target"></param>
    public static void FinalizeSpawn(GameObject target)
    {
        if (NetworkServer.active && target.TryGetComponent(out NetworkIdentity identity))
        {
            NetworkServer.Spawn(target);
        }
    }

    public static void Despawn(GameObject target)
    {
        foreach (var spawnable in target.GetComponents<ISpawnCallbacks>())
        {
            spawnable.OnBeforeDespawn();
        }
        Destroy(target);
    }

    public static bool CanPredictSpawn(GameObject prefab)
    {
        return prefab.GetComponent<Predictable>() && prefab.GetComponent<NetworkIdentity>();
    }

    public static SpawnPrediction MakeSpawnPrediction()
    {
        return new SpawnPrediction() { startId = (ushort)((singleton.localPlayerId << 8) | singleton.nextClientPredictionId) };
    }

    public static void SetLocalPlayerId(byte localPlayerId)
    {
        singleton.localPlayerId = localPlayerId;
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
                    predictable.isPrediction = false;
                    predictable.wasPredicted = true;
                    return predictable.gameObject;
                }
            }

            return Spawn(prefabByGuid[spawnMessage.assetId], spawnMessage.position, spawnMessage.rotation);
        }
        else
        {
            return Spawn(NetworkClient.spawnableObjects[spawnMessage.sceneId].gameObject, spawnMessage.position, spawnMessage.rotation);
        }
    }

    private void UnspawnHandler(GameObject target)
    {
        foreach (var spawnable in target.GetComponents<ISpawnCallbacks>())
        {
            spawnable.OnBeforeDespawn();
        }
        Destroy(target);
    }

    private void PostSpawnHandler(GameObject target)
    {
        // inform successful predictions that they're ready!
        if (target.TryGetComponent(out Predictable predictable))
        {
            if (predictable.wasPredicted)
                predictable.onPredictionSuccessful?.Invoke();
        }
    }
}

public interface ISpawnCallbacks
{
    void OnBeforeDespawn();
}