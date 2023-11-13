using UnityEngine;

public class TeamBase : MonoBehaviour
{
    public PlayerTeam team;

    private void OnTriggerStay(Collider other)
    {
        if (Mirror.NetworkServer.active)
        {
            if (other.TryGetComponent(out Character player) && GameState.Get(out GameStateTeamFlags stateCTF))
            {
                TheFlag ourFlag = stateCTF.GetTeamFlag(team);

                if (player.holdingFlag != null && player.team == team && ourFlag?.carryable.state == Carryable.State.Idle)
                {
                    player.holdingFlag.Capture(player);
                }
            }
        }
    }
}
