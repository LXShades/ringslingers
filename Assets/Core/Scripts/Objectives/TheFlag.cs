using Mirror;
using UnityEngine;

public class TheFlag : NetworkBehaviour
{
    public enum State
    {
        Idle,
        Dropped,
        Carrying
    }

    [Header("Flag setup")]
    public PlayerTeam team;

    public GameSound pickupSound;


    [Header("Drop")]
    public float dropHorizontalVelocity = 8f;
    public float dropVerticalVelocity = 8f;

    public float dropRespawnCountdownDuration = 15f;

    // State
    [SyncVar]
    private State state = State.Idle;

    // Dropped state
    private float dropRespawnCountdown;

    // Carrying
    public int currentCarrier { get => _currentCarrier; set => _currentCarrier = value; }

    private int attachedToPlayer = -1;

    [SyncVar]
    private int _currentCarrier = -1;

    // Spawning
    private Vector3 basePosition;

    // Components
    private Movement movement;
    private Blinker blinker;

    private void Awake()
    {
        basePosition = transform.position;
        movement = GetComponent<Movement>();
        blinker = GetComponent<Blinker>();
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
        }

        switch (state)
        {
            case State.Dropped:
                // respawn sequence
                dropRespawnCountdown -= Time.deltaTime;

                if (dropRespawnCountdown <= 0f)
                {
                    MessageFeed.Post($"The {team.ToColoredString()} flag has returned to base.");

                    ReturnToBase();
                }

                movement.SimulateDefault(Time.deltaTime);
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (NetworkServer.active)
        {
            Player player = other.GetComponent<Player>();

            if (player && !player.damageable.isInvincible)
            {
                // pick up the flag
                if (currentCarrier == -1 && player.team != team)
                {
                    state = State.Carrying;
                    currentCarrier = player.playerId;
                    movement.useManualPhysics = true;

                    MessageFeed.Post($"<player>{player.playerName}</player> picked up the {team.ToColoredString()} flag!");
                }

                // return the slab
                // ...flag
                if (currentCarrier == -1 && state == State.Dropped && player.team == team)
                {
                    MessageFeed.Post($"<player>{player.playerName}</player> returned the {team.ToColoredString()} flag to base!");

                    ReturnToBase();
                }
            }
        }
    }

    public void ReturnToBase()
    {
        transform.SetParent(null, false);
        transform.position = basePosition;
        state = State.Idle;

        dropRespawnCountdown = 0f;

        attachedToPlayer = -1;

        blinker.timeRemaining = 0f;

        if (NetworkServer.active)
            currentCarrier = -1;
    }

    public void Drop()
    {
        if (NetworkServer.active)
        {
            if (currentCarrier != -1)
            {
                Player carryingPlayer = Netplay.singleton.players[currentCarrier];

                if (carryingPlayer != null)
                {
                    // begin movement
                    Vector2 dropDirection = Random.insideUnitCircle;

                    transform.SetParent(null, false);
                    transform.position = carryingPlayer.transform.position;
                    movement.velocity = new Vector3(dropDirection.x * dropHorizontalVelocity, dropDirection.y * dropVerticalVelocity, dropDirection.y * dropHorizontalVelocity);

                    // start countdown
                    dropRespawnCountdown = dropRespawnCountdownDuration;

                    // drop state
                    currentCarrier = -1;
                    attachedToPlayer = -1;
                    state = State.Dropped;

                    RpcDrop(dropRespawnCountdown);

                    MessageFeed.Post($"<player>{carryingPlayer.playerName}</player> dropped the {team.ToColoredString()} flag.");
                }
            }
            else
            {
                Log.WriteWarning("Can't drop the flag, it's not being carried!");
            }
        }
    }

    private void RpcDrop(float blinkTime)
    {
        blinker.timeRemaining = blinkTime;
    }
}
