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

    private Coroutine respawnRoutine = null;

    void Start()
    {
        originalLayer = gameObject.layer;
        originalPosition = transform.position;
    }

    public void Despawn()
    {
        if (NetworkServer.active && GameManager.singleton.itemRespawnTime > 0 && isSpawned)
        {
            isSpawned = false;

            StopCoroutine(nameof(RespawnRoutine));
            respawnRoutine = StartCoroutine(nameof(RespawnRoutine));
        }
    }

    public void Respawn()
    {
        isSpawned = true;
    }

    private void OnIsSpawnedChanged(bool oldVal, bool newVal)
    {
        _isSpawned = newVal;

        if (swapLayerOnRespawn)
        {
            gameObject.layer = newVal ? originalLayer : despawnedLayer;

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

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(GameManager.singleton.itemRespawnTime);
        Respawn();
    }
}
