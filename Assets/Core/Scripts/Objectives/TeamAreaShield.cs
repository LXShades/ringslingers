using Mirror;
using UnityEngine;

public class TeamAreaShield : NetworkBehaviour, IMovementCollisionCallbacks
{
    public PlayerTeam team;
    public ShardHolder[] shardHolderPowerSources;

    private bool isShieldUp = true;

    private Renderer shieldRenderer;

    private void Awake()
    {
        shieldRenderer = GetComponent<Renderer>();
    }

    public void OnMovementCollidedBy(Movement source, TickInfo tickInfo)
    {
    }

    public bool ShouldBlockMovement(Movement source, in RaycastHit hit)
    {
        if (!isShieldUp)
            return false;

        if (source.TryGetComponent(out Character character))
            return character.team != team && Vector3.Dot(character.movement.velocity, character.transform.position - transform.position) <= 0f; // you can always leave, but not always enter
        else
            return true;
    }

    private void Update()
    {
        isShieldUp = false;
        foreach (var shard in shardHolderPowerSources)
        {
            if (shard.currentNumShards > 0)
                isShieldUp = true;
        }

        if (isShieldUp != shieldRenderer.enabled)
            shieldRenderer.enabled = isShieldUp;
    }
}
