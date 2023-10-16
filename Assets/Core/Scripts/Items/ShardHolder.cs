using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class ShardHolder : NetworkBehaviour, IMovementCollisionCallbacks
{
    public static List<ShardHolder> all = new List<ShardHolder>();

    [Header("Identification (row=how far into team base)")]
    public PlayerTeam team;
    public int row;
    public int column;

    [Header("Rewards")]
    public ItemActivatorVolume[] itemActivatorsByShardCount = new ItemActivatorVolume[0];

    [Header("Sounds")]
    public GameSound addShardSound;
    public GameSound smashSound;

    [Header("Hierarchy")]
    public Damageable damageable;
    public GameObject[] shardPieceSlots = new GameObject[0];

    [Header("Beacon")]
    public Gradient beaconColourOverHealth;
    public Renderer beaconRenderer;

    [Header("Stats")]
    public int initialNumShards = 0;
    private int maxNumShards => int.MaxValue; // hack for now - do not set a real limit until we have a solution to the 'issue' (if a shard despawns/returns, and its home is at max capacity, where does it go?)

    [Header("Shards")]
    public GameObject shardPiecePrefab;
    public float shardDropVerticalForce = 8f;
    public float shardDropHorizontalForce = 6f;

    [SyncVar(hook = nameof(OnCurrentNumShardsChanged))]
    public int currentNumShards;

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentNumShards = initialNumShards;
        RefreshShardCount();
    }

    private void Awake()
    {
        all.Add(this);
    }

    private void OnDestroy()
    {
        all.Remove(this);
    }

    private void Start()
    {
        damageable.onLocalDamaged.AddListener(OnDamaged);
    }

    private void ServerSpawnShardPiece()
    {
        if (NetworkServer.active)
        {
            GameObject shardPiece = Spawner.Spawn(shardPiecePrefab, transform.position + new Vector3(0f, 2f, 0f), Quaternion.identity);

            Vector2 randomVec = UnityEngine.Random.insideUnitCircle;
            shardPiece.GetComponent<ShardPiece>().serverSourceShardHolder = this;
            shardPiece.GetComponent<Carryable>().Drop();
            shardPiece.GetComponent<Movement>().velocity = new Vector3(randomVec.x * shardDropHorizontalForce, shardDropVerticalForce, randomVec.y * shardDropHorizontalForce);
        }
    }

    public void ServerReturnShardPiece(ShardPiece shardPiece)
    {
        Spawner.Despawn(shardPiece.gameObject);
        currentNumShards++;
        RpcPlayAddShardSound();
    }

    private void RefreshShardCount()
    {
        for (int i = 0; i < shardPieceSlots.Length; i++)
            shardPieceSlots[i].SetActive(i < currentNumShards);
        for (int i = 0; i < itemActivatorsByShardCount.Length; i++)
        {
            if (itemActivatorsByShardCount[i])
                itemActivatorsByShardCount[i].SetItemsEnabled(i < currentNumShards);
        }

        beaconRenderer.material.color = beaconColourOverHealth.Evaluate((float)currentNumShards / initialNumShards);
    }

    private void OnDamaged(GameObject source, Vector3 direction, bool dunno)
    {
        if (NetworkServer.active && currentNumShards > 0)
        {
            currentNumShards--;

            ServerSpawnShardPiece();

            RpcPlaySmashSound();
        }
    }

    private void OnCurrentNumShardsChanged(int oldNum, int newNum)
    {
        RefreshShardCount();
    }

    [ClientRpc]
    private void RpcPlaySmashSound() => GameSounds.PlaySound(gameObject, smashSound);
    [ClientRpc]
    private void RpcPlayAddShardSound() => GameSounds.PlaySound(gameObject, addShardSound);

    private void OnValidate()
    {
        if (initialNumShards > shardPieceSlots.Length)
            Debug.LogError("Initial num shards is larger than actual number of slots, which defines the maximum number of visual shards in this holder", gameObject);
    }

    public bool ShouldBlockMovement(Movement source, in RaycastHit hit) => true;

    public void OnMovementCollidedBy(Movement source, TickInfo tickInfo)
    {
        if (NetworkServer.active && source.TryGetComponent(out Character character) && currentNumShards < maxNumShards)
        {
            // Add a shard piece to the pile if the player is carrying one
            List<Carryable> carriedCarryables = Carryable.GetAllCarriedByPlayer(character);
            if (carriedCarryables.Find(x => x.TryGetComponent<ShardPiece>(out _)) is Carryable carriedShardPiece)
            {
                ServerReturnShardPiece(carriedShardPiece.GetComponent<ShardPiece>());
            }
        }
    }
}
