using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public struct SpawnPrediction
    {
        public ushort startId;
    }

    public static Spawner singleton => _singleton ?? (_singleton = FindObjectOfType<Spawner>());
    private static Spawner _singleton;

    private readonly Dictionary<ushort, Predictable> predictedSpawns = new Dictionary<ushort, Predictable>();
    private readonly Dictionary<Guid, GameObject> prefabByGuid = new Dictionary<Guid, GameObject>();

    public List<GameObject> spawnablePrefabs = new List<GameObject>();
    
    private bool isPredictingSpawn = false;
    private SpawnPrediction activeSpawnPrediction;

    private byte nextClientPredictionId = 0;
    private ushort nextServerPredictionId = 0;

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
        if (NetworkServer.active)
        {
            foreach (GameObject obj in scene.GetRootGameObjects())
            {
                foreach (Transform child in obj.GetComponentsInChildren<Transform>())
                {
                    if (child.GetComponent<NetworkIdentity>())
                    {
                        SyncActionSystem.RegisterSyncActions(child.gameObject);
                    }
                }
            }
        }

        RegisterSpawnHandlers();
    }

    /// <summary>
    /// Starts an object spawn sequence, allowing time for SyncVars to be set
    /// </summary>
    public static GameObject StartSpawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return Instantiate(prefab, position, rotation);
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

    /// <summary>
    /// Finalizes and networks a spawn, sending relevant info to clients.
    /// </summary>
    /// <param name="target"></param>
    public static void FinalizeSpawn(GameObject target)
    {
        if (NetworkServer.active && target.TryGetComponent(out NetworkIdentity identity))
        {
            if (target.GetComponent<Predictable>() != null)
            {
                identity.predictedId = singleton.nextServerPredictionId;
                singleton.nextServerPredictionId = (ushort)((singleton.nextServerPredictionId & ~0xFF) | ((singleton.nextServerPredictionId + 1) & 0xFF));
            }

            NetworkServer.Spawn(target);
            SyncActionSystem.RegisterSyncActions(target);
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

    public static void StartSpawnPrediction()
    {
        singleton.isPredictingSpawn = true;
        singleton.activeSpawnPrediction = new SpawnPrediction() { startId = (ushort)((Netplay.singleton.localPlayerId << 8) | singleton.nextClientPredictionId) };
    }

    public static SpawnPrediction EndSpawnPrediction()
    {
        singleton.isPredictingSpawn = false;
        return singleton.activeSpawnPrediction;
    }

    public static void ApplySpawnPrediction(SpawnPrediction prediction)
    {
        singleton.nextServerPredictionId = prediction.startId;
    }

    /// <summary>
    /// Spawns a predicted object
    /// </summary>
    [Client]
    public static GameObject PredictSpawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!singleton.isPredictingSpawn)
            Log.WriteWarning("You should call StartSpawnPrediction before predicting a spawn and send the result to the server after EndSpawnPrediction.");

        GameObject obj = Instantiate(prefab, position, rotation);

        if (obj && obj.TryGetComponent(out NetworkIdentity identity) && obj.TryGetComponent(out Predictable predictable))
        {
            // player-specific "predictable object ID" which we can use later to replace the object
            identity.predictedId = (ushort)((Netplay.singleton.localPlayerId << 8) | singleton.nextClientPredictionId);

            predictable.isPrediction = true;

            singleton.predictedSpawns[identity.predictedId] = predictable;
            singleton.nextClientPredictionId++;
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
                    predictable.isPrediction = false;
                    predictable.wasPredicted = true;
                    return predictable.gameObject;
                }
            }

            Log.Write($"Spawning a {prefabByGuid[spawnMessage.assetId]}");
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
        SyncActionSystem.RegisterSyncActions(target);

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