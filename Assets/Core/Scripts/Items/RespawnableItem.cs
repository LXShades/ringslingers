using Mirror;
using System;
using System.Collections;
using UnityEngine;

public class RespawnableItem : NetworkBehaviour
{
    [Header("Default behaviour")]
    public bool swapLayerOnRespawn = true;
    [Layer]
    public int despawnedLayer;
    public GameObject[] despawnedLayerAffectedSubobjects = new GameObject[0];

    private int originalLayer;

    private Vector3 originalPosition;

    public bool isSpawned
    {
        get
        {
            return _isSpawned;
        }
        set
        {
            if (value != _isSpawned && NetworkServer.active)
                OnIsSpawnedChanged(isSpawned, value);
        }
    }

    [SyncVar(hook = nameof(OnIsSpawnedChanged))]
    private bool _isSpawned = true;

    public Action onRespawn;
    public Action onDespawn;

    private float timeTilRespawn = 0f;
    private Coroutine respawnRoutine = null;

    public bool isRespawnPaused { get; set; }

    void Start()
    {
        originalLayer = gameObject.layer;
        originalPosition = transform.position;
    }

    public void Despawn()
    {
        DespawnForSeconds(GameManager.singleton.itemRespawnTime);
    }

    public void DespawnForSeconds(float numSeconds)
    {
        if (NetworkServer.active)
        {
            timeTilRespawn = numSeconds;

            if (isSpawned)
            {
                isSpawned = false;

                StopRespawnRoutine(); // just in case...?
                respawnRoutine = StartCoroutine(nameof(RespawnRoutine));
            }
        }
    }

    public void Respawn()
    {
        isSpawned = true;
        StopRespawnRoutine();
    }

    public void SetSpawnPosition(Vector3 position)
    {
        originalPosition = position;
    }

    private void OnIsSpawnedChanged(bool oldVal, bool newVal)
    {
        _isSpawned = newVal;

        if (swapLayerOnRespawn)
        {
            gameObject.layer = newVal ? originalLayer : despawnedLayer;
            foreach (GameObject obj in despawnedLayerAffectedSubobjects)
                obj.layer = newVal ? originalLayer : despawnedLayer;

            if (newVal)
            {
                transform.position = originalPosition;
            }
        }

        if (oldVal != newVal)
        {
            if (newVal)
                onRespawn?.Invoke();
            else
                onDespawn?.Invoke();
        }
    }

    private IEnumerator RespawnRoutine()
    {
        while (!isSpawned)
        {
            yield return new WaitForSeconds(1f);

            if (!isRespawnPaused)
                timeTilRespawn--;

            if (!isSpawned && timeTilRespawn <= 0f)
            {
                Respawn();
                break;
            }
        }
        respawnRoutine = null;
    }

    private void StopRespawnRoutine()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }
    }
}
