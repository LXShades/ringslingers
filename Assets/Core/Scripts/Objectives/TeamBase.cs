using UnityEngine;

public class TeamBase : MonoBehaviour
{
    public PlayerTeam team;

    private void OnTriggerStay(Collider other)
    {
        if (Mirror.NetworkServer.active)
        {
            if (other.TryGetComponent(out Player player) && NetGameState.singleton is NetGameStateCTF stateCTF)
            {
                TheFlag playerFlag = null;
                TheFlag ourFlag = stateCTF.GetTeamFlag(team);
                if (stateCTF.redFlag.currentCarrier == player.playerId)
                    playerFlag = stateCTF.redFlag;
                else if (stateCTF.blueFlag.currentCarrier == player.playerId)
                    playerFlag = stateCTF.blueFlag;


                if (playerFlag != null && playerFlag.team != team && ourFlag?.currentCarrier == -1)
                {
                    MessageFeed.Post($"<player>{player.playerName}</player> captured the flag!");

                    stateCTF.AwardPoint(team);
                    player.score += stateCTF.playerPointsPerCapture;
                    playerFlag.ReturnToBase();
                }
            }
        }
    }
}
