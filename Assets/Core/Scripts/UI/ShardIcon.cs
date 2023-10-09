using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShardIcon : MonoBehaviour
{
    private Image image;
    private ShardHolder shardHolder;

    public PlayerTeam team;
    public int column;
    public int row;

    private int lastNumShards = -1;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void Update()
    {
        if (shardHolder == null)
        {
            FindAssociatedShardHolder();
        }

        if (shardHolder != null)
        {
            if (shardHolder.currentNumShards != lastNumShards)
            {
                image.color = new Color(1f, 1f, 1f, ((float)shardHolder.currentNumShards / shardHolder.initialNumShards));
                lastNumShards = shardHolder.currentNumShards;
            }
        }
    }

    private void FindAssociatedShardHolder()
    {
        foreach (ShardHolder shardHolderCandidate in ShardHolder.all)
        {
            if (shardHolderCandidate.team == team && shardHolderCandidate.column == column && shardHolderCandidate.row == row)
            {
                shardHolder = shardHolderCandidate;
                break;
            }
        }
    }
}
