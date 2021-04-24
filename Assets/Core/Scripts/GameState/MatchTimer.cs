using Mirror;
using UnityEngine;

public class MatchTimer : MatchStateComponent
{
    [Header("Timer settings")]
    public float timeLimit = 5f;

    [Header("Networking settings")]
    public int secondsPerTimeUpdate = 5;

    public float timeRemaining { get; private set; }

    public override void OnAwake()
    {
        timeRemaining = timeLimit * 60f;
    }

    public override void OnUpdate()
    {
        if (timeLimit > 0f)
        {
            if (NetworkServer.active && timeRemaining > 0f)
            {
                if ((int)(timeRemaining / secondsPerTimeUpdate - Time.deltaTime) != ((int)(timeRemaining / secondsPerTimeUpdate)))
                    RpcTimeUpdate(timeRemaining);

                if (timeRemaining - Time.deltaTime <= 0f)
                    MatchState.singleton.ServerEndGame(); // end game when time runs out
            }

            timeRemaining = Mathf.Max(timeRemaining - Time.deltaTime, 0f);
        }
        else
        {
            if (NetworkServer.active && (int)(timeRemaining / secondsPerTimeUpdate + Time.deltaTime) != ((int)(timeRemaining / secondsPerTimeUpdate)))
                RpcTimeUpdate(timeRemaining);

            timeRemaining += Time.deltaTime;
        }
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcTimeUpdate(float time)
    {
        if (!NetworkServer.active)
            timeRemaining = time;
    }
}
