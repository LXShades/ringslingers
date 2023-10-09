using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShardPiece : MonoBehaviour
{
    public ShardHolder serverSourceShardHolder;

    private Carryable carryable;

    private void Awake()
    {
        carryable = GetComponent<Carryable>();

        carryable.onDropExpiredServer += ServerOnDropExpired;
    }

    private void ServerOnDropExpired()
    {
        serverSourceShardHolder.ServerReturnShardPiece(this);
    }
}
