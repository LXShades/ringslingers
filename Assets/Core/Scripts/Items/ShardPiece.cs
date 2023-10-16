using Mirror;

public class ShardPiece : NetworkBehaviour
{
    public GameSound pickupSound;
    public ShardHolder serverSourceShardHolder;

    private Carryable carryable;

    private void Awake()
    {
        carryable = GetComponent<Carryable>();

        carryable.onDropExpiredServer += ServerOnDropExpired;
        carryable.onAttemptPickupServer += ServerOnPickup;
    }

    private bool ServerOnPickup(Character character)
    {
        RpcPlayPickupSound();
        return true;
    }

    private void ServerOnDropExpired()
    {
        serverSourceShardHolder.ServerReturnShardPiece(this);
    }

    [ClientRpc]
    private void RpcPlayPickupSound() => GameSounds.PlaySound(gameObject, pickupSound);
}
