using UnityEngine;

public class TeamBase : MonoBehaviour
{
    public PlayerTeam team;

    private void OnTriggerStay(Collider other)
    {
        if (Mirror.NetworkServer.active)
        {
            if (other.TryGetComponent(out Character player) && MatchState.Get(out MatchFlags stateCTF))
            {
                TheFlag ourFlag = stateCTF.GetTeamFlag(team);

                if (player.holdingFlag != null && player.team == team && ourFlag?.state == TheFlag.State.Idle)
                {
                    player.holdingFlag.Capture(player);
                }
            }
        }
    }
}
