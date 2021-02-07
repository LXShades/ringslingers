using Mirror;
using UnityEngine;

public class NetGameStateDeathmatch : NetGameState
{
    [Header("Match settings")]
    public float timeLimit = 5f;
    public int intermissionTime = 15;

    [Header("Networking settings")]
    public int secondsPerTimeUpdate = 5;

    public float timeRemaining { get; private set; }

    public float timeTilRestart => timeRemaining < 0f ? intermissionTime + timeRemaining : 0f;

    public override bool HasRoundFinished => timeRemaining <= 0f;

    public override void OnAwake()
    {
        base.OnAwake();

        timeRemaining = timeLimit * 60;
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

            if ((timeRemaining <= -intermissionTime) != (timeRemaining - Time.deltaTime <= -intermissionTime))
            {
                NetMan.singleton.ServerChangeScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, true);
            }
        }

        timeRemaining -= Time.deltaTime;
    }

    [ClientRpc]
    private void RpcTimeUpdate(float time)
    {
        if (!NetworkServer.active)
            timeRemaining = time;
    }
}