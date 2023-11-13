using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class TheFlag : NetworkBehaviour
{
    public enum FlagSoundIndex
    {
        Pickup,
        Returned,
        Captured
    }

    [Header("Flag setup")]
    public PlayerTeam team;

    public GameObject gotFlagIndicator;
    public float gotFlagIndicatorHoverHeight = 2f;

    public GameSound pickupSound;
    public GameSound returnedSound;
    public GameSound capturedByEnemySound;
    public GameSound capturedByAllySound;

    [Header("Dropping")]
    public float dropHorizontalVelocity = 8f;
    public float dropVerticalVelocity = 8f;

    public float dropMovementSyncRate = 3f;

    public int currentCarrier => carryable.currentCarrier;

    // Spawning
    private Vector3 basePosition;
    private Quaternion baseRotation;

    // Components
    private Movement movement;
    private SyncMovement syncMovement;
    private List<Renderer> renderers;
    public Carryable carryable { get; private set; }

    private void Awake()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        movement = GetComponent<Movement>();
        syncMovement = GetComponent<SyncMovement>();
        carryable = GetComponent<Carryable>();

        carryable.onAttemptPickupServer += ServerOnAttemptPickup;
        carryable.onDropExpiredServer += ServerOnDropExpired;
        carryable.onLostPlayer += () => ReturnToBase(true);
        carryable.onDropped += OnDropped;
    }

    private void Start()
    {
        if (GameState.Get(out GameStateTeamFlags stateCTF))
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
        if (carryable.state == Carryable.State.Dropped)
            movement.SimulateBasicPhysics(Time.deltaTime);

        syncMovement.updatesPerSecond = carryable.state == Carryable.State.Dropped ? dropMovementSyncRate : 0f;
    }

    private void LateUpdate()
    {
        if (carryable.state == Carryable.State.Carrying)
        {
            Character carryingCharacter = carryable.currentCarrierCharacter;
            if (carryingCharacter)
            {
                bool shouldShowIndicator = carryingCharacter != Netplay.singleton.localPlayer;
                if (gotFlagIndicator.activeSelf != shouldShowIndicator)
                    gotFlagIndicator.SetActive(shouldShowIndicator);

                gotFlagIndicator.transform.SetPositionAndRotation(carryingCharacter.transform.position + Vector3.up * gotFlagIndicatorHoverHeight, Quaternion.LookRotation(gotFlagIndicator.transform.position - GameManager.singleton.camera.transform.position));
            }
        }
        else
        {
            if (gotFlagIndicator.activeSelf)
                gotFlagIndicator.SetActive(false);
        }
    }

    public void Capture(Character player)
    {
        if (NetworkServer.active && GameState.Get(out GameStateTeams stateTeams) && GameState.Get(out GameStateTeamFlags stateCtf))
        {
            MessageFeed.Post($"<player>{player.playerName}</player> captured the {team.ToColoredString()} flag!");

            stateTeams.AwardPoint(player.team);
            player.score += stateCtf.playerPointsPerCapture;
            player.holdingFlag.ReturnToBase(false);

            RpcPlaySound(FlagSoundIndex.Captured);

            carryable.StartInteractionCooldown(carryable.dropInteractionCooldown);
        }
    }

    private void OnDropped(Character character)
    {
        if (NetworkServer.active)
        {
            // begin movement
            Vector2 dropDirection = UnityEngine.Random.insideUnitCircle.normalized;
            movement.velocity = new Vector3(dropDirection.x * dropHorizontalVelocity, dropVerticalVelocity, dropDirection.y * dropHorizontalVelocity);

            // start countdown
            syncMovement.SyncNow();

            MessageFeed.Post($"<player>{(character != null ? character.playerName : "[PLAYER MISSING]")}</player> dropped the {team.ToColoredString()} flag.");
        }
    }

    private bool ServerOnAttemptPickup(Character player)
    {
        // if opposide teams: pick up the flag
        if (player.team != team)
        {

            MessageFeed.Post($"<player>{player.playerName}</player> picked up the {team.ToColoredString()} flag!");
            RpcPlaySound(FlagSoundIndex.Pickup);

            return true;
        }

        // return the slab
        // ...flag
        if (carryable.state == Carryable.State.Dropped && player.team == team)
        {
            MessageFeed.Post($"<player>{player.playerName}</player> returned the {team.ToColoredString()} flag to base!");

            ReturnToBase(false);

            if (Netplay.singleton.localPlayer?.team == team)
                RpcPlaySound(FlagSoundIndex.Returned);
        }

        return false;
    }

    private void ServerOnDropExpired()
    {
        ReturnToBase(true);
    }

    public void ReturnToBase(bool postReturnMessage)
    {
        transform.position = basePosition;
        transform.rotation = baseRotation;

        if (NetworkServer.active)
        {
            carryable.ServerDetachFromPlayer();
            carryable.ServerSetState(Carryable.State.Idle);

            if (postReturnMessage)
                MessageFeed.Post($"The {team.ToColoredString()} flag has returned to base.");

            syncMovement.SyncNow();

            RpcReturnToBase();
        }
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcReturnToBase()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.team == team)
            GameSounds.PlaySound(null, returnedSound);

        carryable.ServerSetState(Carryable.State.Idle);
    }

    [ClientRpc]
    private void RpcPlaySound(FlagSoundIndex soundIndex)
    {
        switch (soundIndex)
        {
            case FlagSoundIndex.Pickup:
                GameSounds.PlaySound(gameObject, pickupSound);
                break;
            case FlagSoundIndex.Returned:
                GameSounds.PlaySound(gameObject, returnedSound);
                break;
            case FlagSoundIndex.Captured:
                if (Netplay.singleton.localPlayer?.team == team)
                    GameSounds.PlaySound(null, capturedByEnemySound);
                else
                    GameSounds.PlaySound(null, capturedByAllySound);
                break;
        }
    }
}
