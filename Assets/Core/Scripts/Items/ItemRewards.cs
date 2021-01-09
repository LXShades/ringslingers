using UnityEngine;

public class ItemRewards : MonoBehaviour
{
    [Header("Rewards")]
    public int numRingsToReward = 10;

    public void ApplyReward(Player player)
    {
        if (Mirror.NetworkServer.active)
        {
            player.numRings += numRingsToReward;
        }
    }
}
