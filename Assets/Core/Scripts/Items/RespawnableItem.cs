using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;

public class RespawnableItem : SyncedObject
{
    public float respawnCountdownTimer
    {
        get; private set;
    }

    public bool isSpawned
    {
        get
        {
            return gameObject.activeSelf;
        }
        set
        {
            gameObject.SetActive(value);

            if (value)
                respawnCountdownTimer = 0;
        }
    }

    public override void FrameUpdate()
    {
        if (respawnCountdownTimer > 0)
        {
            respawnCountdownTimer = Mathf.Max(respawnCountdownTimer - Frame.current.deltaTime, 0);

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
