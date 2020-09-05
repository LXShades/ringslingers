using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerClient : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId;

    void OnPlayerIdChanged(int oldValue, int newValue)
    {
        playerId = newValue;

        if (isLocalPlayer)
        {
            Netplay.singleton.localPlayerId = newValue;
        }
    }
}
