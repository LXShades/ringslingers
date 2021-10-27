using Mirror;

public class MatchTeams : MatchStateComponent
{
    public int pointLimit = 5;

    public int redTeamPoints => _redTeamPoints;
    public int blueTeamPoints => _blueTeamPoints;

    [SyncVar] private int _redTeamPoints;
    [SyncVar] private int _blueTeamPoints;

    public void AwardPoint(PlayerTeam team)
    {
        if (team == PlayerTeam.Red)
            _redTeamPoints++;
        else if (team == PlayerTeam.Blue)
            _blueTeamPoints++;

        if (redTeamPoints >= pointLimit || blueTeamPoints >= pointLimit)
            MatchState.singleton.ServerEndGame();
    }

    public PlayerTeam FindBestTeamToJoin()
    {
        int numReds = 0, numBlues = 0;

        foreach (Character player in Netplay.singleton.players)
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
            return UnityEngine.Random.value > 0.5f ? PlayerTeam.Red : PlayerTeam.Blue;
    }

    public override string GetWinners()
    {
        if (redTeamPoints > blueTeamPoints)
            return "Red team";
        else if (blueTeamPoints > redTeamPoints)
            return "Blue team";
        else
            return "Both teams";
    }
}
