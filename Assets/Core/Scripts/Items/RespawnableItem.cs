using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;

public class RespawnableItem : WorldObjectComponent
{
    private int originalLayer;
    private int despawnedLayer;

    private Vector3 originalPosition;

    public float respawnCountdownTimer
    {
        get; private set;
    }

    public bool isSpawned
    {
        get
        {
            return gameObject.layer != despawnedLayer;
        }
        set
        {
            if (value != isSpawned)
            {
                gameObject.layer = value ? originalLayer : despawnedLayer;

                if (value)
                {
                    respawnCountdownTimer = 0;
                    transform.position = originalPosition;
                }
                else
                {
                    gameObject.SetActive(false); // Unity bug: physics scene won't update unless we do something fun
                    gameObject.SetActive(true);
                }
            }
        }
    }

    public override void WorldStart()
    {
        originalLayer = gameObject.layer;
        despawnedLayer = LayerMask.NameToLayer("Despawned");
        originalPosition = transform.position;
    }

    public override void WorldUpdate(float deltaTime)
    {
        if (respawnCountdownTimer > 0)
        {
            respawnCountdownTimer = Mathf.Max(respawnCountdownTimer - deltaTime, 0);

            if (respawnCountdownTimer <= 0)
                Respawn();
        }
    }

    public void Pickup()
    {
        if (GameManager.singleton.itemRespawnTime > 0)
        {
            isSpawned = false;
            respawnCountdownTimer = GameManager.singleton.itemRespawnTime;
        }
    }

    public void Respawn()
    {
        isSpawned = true;
    }
}
