using Mirror;
using UnityEngine;

public class NetGameStateCTF : NetGameState
{
    [Header("Match settings")]
    public int pointLimit = 5;
    public int intermissionTime = 15;
    public int playerPointsPerCapture = 250;

    [Header("Networking settings")]
    public int secondsPerTimeUpdate = 5;

    public float timeRemaining { get; private set; }

    public float timeTilRestart => timeRemaining < 0f ? intermissionTime + timeRemaining : 0f;

    public int redTeamPoints => _redTeamPoints;
    public int blueTeamPoints => _blueTeamPoints;

    [SyncVar] private int _redTeamPoints;
    [SyncVar] private int _blueTeamPoints;

    public TheFlag redFlag { get; set; }
    public TheFlag blueFlag { get; set; }

    public override bool HasRoundFinished => redTeamPoints >= pointLimit || blueTeamPoints >= pointLimit;

    public override void OnAwake()
    {
        base.OnAwake();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (NetworkServer.active)
        {
            if (HasRoundFinished)
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
            _redTeamPoints++;
        }
        else if (team == PlayerTeam.Blue)
        {
            _blueTeamPoints++;
        }
    }

    public TheFlag GetTeamFlag(PlayerTeam team)
    {
        if (team == PlayerTeam.Red)
            return redFlag;
        else if (team == PlayerTeam.Blue)
            return blueFlag;
        else
            return null;
    }

    public PlayerTeam FindBestTeamToJoin()
    {
        int numReds = 0, numBlues = 0;

        foreach (Player player in Netplay.singleton.players)
        {
            if (player != null)
            {
                if (player.team == PlayerTeam.Red)
                    numReds++;
                if (player.team == PlayerTeam.Blue)
                    numBlues++;
            }
        }

        if (numReds > numBlues)
            return PlayerTeam.Blue;
        else if (numBlues > numReds)
            return PlayerTeam.Red;
        else
        {
            // we could go random, but it would be nice for this to be more predictable, so here's a "tag team" strategy
            return ((numReds / 2) & 1) == 0 ? PlayerTeam.Red : PlayerTeam.Blue;
        }
    }

    [ClientRpc]
    private void RpcTimeUpdate(float time)
    {
        if (!NetworkServer.active)
            timeRemaining = time;
    }
}