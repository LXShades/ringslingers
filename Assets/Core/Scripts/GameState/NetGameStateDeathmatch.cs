using Mirror;
using UnityEngine;

public class NetGameStateCTF : NetGameState
{
    [Header("Match settings")]
    public int pointLimit = 5;
    public int intermissionTime = 15;

    [Header("Networking settings")]
    public int secondsPerTimeUpdate = 5;

    public float timeRemaining { get; private set; }

    public float timeTilRestart => timeRemaining < 0f ? intermissionTime + timeRemaining : 0f;

    public int redTeamPoints { get; private set; }
    public int blueTeamPoints { get; private set; }

    public override void OnAwake()
    {
        base.OnAwake();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (NetworkServer.active)
        {
            if (redTeamPoints >= pointLimit || blueTeamPoints >= pointLimit)
            {
                timeRemaining -= Time.deltaTime;
                if ((int)(timeRemaining / secondsPerTimeUpdate - Time.deltaTime) != ((int)(timeRemaining / secondsPerTimeUpdate)))
                {
                    RpcTimeUpdate(timeRemaining);
                }

                if (timeRemaining < -intermissionTime)
                {
                    NetMan.singleton.ServerChangeScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                }
            }
        }
    }

    public void AwardPoint(PlayerTeam team)
    {
        if (team == PlayerTeam.Red)
        {
            redTeamPoints++;
        }
        else if (team == PlayerTeam.Blue)
        {
            blueTeamPoints++;
        }
    }

    [ClientRpc]
    private void RpcTimeUpdate(float time)
    {
        if (!NetworkServer.active)
            timeRemaining = time;
    }
}
