using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShardHolder : NetworkBehaviour
{
    [Header("Hierarchy")]
    public Damageable damageable;
    public GameObject[] shardPieceSlots = new GameObject[0];

    [Header("Stats")]
    public int initialNumShards = 0;

    [Header("Shards")]
    public GameObject shardPiecePrefab;
    [SyncVar(hook = nameof(OnCurrentNumShardsChanged))]
    public int currentNumShards;

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentNumShards = initialNumShards;
        RefreshVisibleShards();
    }

    private void Start()
    {
        damageable.onLocalDamaged.AddListener(OnDamaged);
    }

    private void SpawnShardPiece()
    {

    }

    private void RefreshVisibleShards()
    {
        for (int i = 0; i < shardPieceSlots.Length; i++)
            shardPieceSlots[i].SetActive(i < currentNumShards);
    }

    private void OnDamaged(GameObject source, Vector3 direction, bool dunno)
    {
        if (NetworkServer.active && currentNumShards > 0)
        {
            currentNumShards--;

            SpawnShardPiece();
        }
    }

    private void OnCurrentNumShardsChanged(int oldNum, int newNum)
    {
        RefreshVisibleShards();
    }

    private void OnValidate()
    {
        if (initialNumShards > shardPieceSlots.Length)
            Debug.LogError("Initial num shards is larger than actual number of slots, which should define the maximum number of shards in this holder", gameObject);
    }
}
