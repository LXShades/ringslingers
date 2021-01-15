using Mirror;
using UnityEngine;

public class NetGameStateDeathmatch : NetGameState
{
    [Header("Match settings")]
    public int timeLimit = 5;

    [Header("Networking settings")]
    public int secondsPerTimeUpdate = 5;

    public float timeRemaining { get; private set; }

    public override void OnAwake()
    {
        base.OnAwake();

        timeRemaining = timeLimit;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (NetworkServer.active)
        {
            if ((int)(timeRemaining / secondsPerTimeUpdate - Time.deltaTime) != ((int)(timeRemaining / secondsPerTimeUpdate)))
            {
                RpcTimeUpdate(timeRemaining);
            }
        }

        timeRemaining -= Time.deltaTime;
    }

    [ClientRpc]
    private void RpcTimeUpdate(float time)
    {
        timeRemaining = time;
    }
}
