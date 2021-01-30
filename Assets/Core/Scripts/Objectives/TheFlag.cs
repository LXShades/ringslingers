using Mirror;
using UnityEngine;

public class TheFlag : NetworkBehaviour
{
    public PlayerTeam team;

    public GameSound pickupSound;

    [SyncVar(hook=nameof(OnCarrierChanged))]
    public int currentCarrier = -1;

    private int attachedToPlayer;

    private Vector3 basePosition;

    private void Awake()
    {
        basePosition = transform.position;
    }

    private void Start()
    {
        if (NetGameState.singleton is NetGameStateCTF stateCTF)
        {
            if (team == PlayerTeam.Red)
                stateCTF.redFlag = this;
            else if (team == PlayerTeam.Blue)
                stateCTF.blueFlag = this;
        }
        else
        {
            Log.WriteWarning("Cannot register flag - game state is not CTF");
        }
    }

    private void Update()
    {
        if (attachedToPlayer != currentCarrier)
        {
            if (currentCarrier != -1)
            {
                Player player = Netplay.singleton.players[currentCarrier];

                if (player)
                {
                    transform.SetParent(player.flagHoldBone, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    attachedToPlayer = currentCarrier;
                }
                else
                {
                    // uh, that's weird, no one's carrying it?
                    ReturnToBase();
                }
            }
            else
            {
                ReturnToBase();
                attachedToPlayer = -1;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkServer.active)
        {
            if (currentCarrier == -1 && other.TryGetComponent(out Player player) && player.team != team && !player.damageable.isInvincible)
            {
                currentCarrier = player.playerId;

                MessageFeed.Post($"<player>{player.playerName}</player> picked up the {team} flag!");
            }
        }
    }

    public void ReturnToBase()
    {
        transform.SetParent(null, false);
        transform.position = basePosition;

        if (NetworkServer.active)
        {
            currentCarrier = -1;
        }
    }
}
