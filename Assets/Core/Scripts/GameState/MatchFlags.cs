using UnityEngine;

[RequireComponent(typeof(MatchTeams))]
public class MatchFlags : MatchStateComponent
{
    [Header("CTF settings")]
    public int playerPointsPerCapture = 250;

    public TheFlag redFlag { get; set; }
    public TheFlag blueFlag { get; set; }

    public TheFlag GetTeamFlag(PlayerTeam team)
    {
        if (team == PlayerTeam.Red)
            return redFlag;
        else if (team == PlayerTeam.Blue)
            return blueFlag;
        else
            return null;
    }
}