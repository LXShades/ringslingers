/// <summary>
/// Makes the highest-scoring player the winner
/// </summary>
public class MatchHighestScoreWinner : MatchStateComponent
{
    public override string GetWinners()
    {
        Player winningPlayer = null;

        foreach (Player candidate in Netplay.singleton.players)
        {
            if (candidate != null)
            {
                if (winningPlayer == null || candidate.score > winningPlayer.score)
                    winningPlayer = candidate;
            }
        }

        return winningPlayer?.playerName;
    }
}
