using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shield : NetworkBehaviour
{
    [SyncVar]
    private GameObject _target;

    public GameObject target
    {
        set => _target = value;
        get => _target;
    }

    private void LateUpdate()
    {
        if (target == null && isServer)
        {
            Spawner.Despawn(gameObject);
            return;
        }

        if (target != null)
        {
            transform.position = target.transform.position + target.transform.up * 0.5f;
        }
    }
}
