/// <summary>
/// Makes the highest-scoring player the winner
/// </summary>
public class GameStateHighestScoreWinner : GameStateComponent
{
    public override string GetWinners()
    {
        Character winningPlayer = null;

        foreach (Character candidate in Netplay.singleton.players)
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
