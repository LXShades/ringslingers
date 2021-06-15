using UnityEngine;

public class ItemRewards : MonoBehaviour
{
    [Header("Rewards")]
    public int numRingsToReward = 10;
    public Shield shieldToReward;

    public void ApplyReward(Character player)
    {
        if (Mirror.NetworkServer.active)
        {
            player.numRings += numRingsToReward;

            if (shieldToReward)
            {
                player.ApplyShield(shieldToReward.gameObject);
            }
        }
    }
}
