﻿using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class TheFlag : NetworkBehaviour, IMovementCollisionCallbacks
{
    public enum State
    {
        Idle,
        Dropped,
        Carrying
    }

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


    [Header("Drop")]
    public float dropHorizontalVelocity = 8f;
    public float dropVerticalVelocity = 8f;

    public float dropRespawnCountdownDuration = 15f;
    public float dropMovementSyncRate = 3f;

    // State
    [SyncVar]
    private State _state = State.Idle;

    public State state
    {
        get => _state;
        private set => _state = value;
    }

    // Dropped state
    private float dropRespawnCountdown;

    // Carrying
    [SyncVar]
    private int _currentCarrier = -1;

    public int currentCarrier { get => _currentCarrier; set => _currentCarrier = value; }

    private int attachedToPlayer = -1;

    private float timeOfLastInteraction = -1f;
    private const float kInteractionCooldown = 0.2f; // basically needed due to physics system quirks and collisions

    // Spawning
    private Vector3 basePosition;
    private Quaternion baseRotation;

    // Components
    private Movement movement;
    private Blinker blinker;
    private SyncMovement syncMovement;
    private List<Renderer> renderers;

    private void Awake()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        movement = GetComponent<Movement>();
        blinker = GetComponent<Blinker>();
        syncMovement = GetComponent<SyncMovement>();
    }

    private void Start()
    {
        if (MatchState.Get(out MatchFlags stateCTF))
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
                Character player = Netplay.singleton.players[currentCarrier];

                if (player)
                {
                    attachedToPlayer = currentCarrier;
                }
                else
                {
                    // uh, that's weird, no one's carrying it?
                    ReturnToBase(true);
                }
            }
            else
            {
                transform.rotation = baseRotation;
                blinker.timeRemaining = 0f;
            }

            attachedToPlayer = currentCarrier;
        }

        switch (state)
        {
            case State.Dropped:
                // respawn sequence
                dropRespawnCountdown -= Time.deltaTime;

                if (NetworkServer.active && dropRespawnCountdown <= 0f)
                    ReturnToBase(true);

                movement.SimulateBasicPhysics(Time.deltaTime);
                break;
        }

        // hide flag when holding it in firstperson
        if (PlayerCamera.singleton.isFirstPerson && PlayerCamera.singleton.currentPlayer != null && currentCarrier == PlayerCamera.singleton.currentPlayer.playerId)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer.enabled)
                    renderer.enabled = false;
            }
        }
        else if (blinker.timeRemaining <= 0f) // blinker interferes with render visibility. so many knobs...
        {
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                    renderer.enabled = true;
            }
        }

        syncMovement.updatesPerSecond = state == State.Dropped ? dropMovementSyncRate : 0f;
    }

    private void LateUpdate()
    {
        if (state == State.Carrying && currentCarrier != -1 && currentCarrier < Netplay.singleton.players.Count)
        {
            Character carryingPlayer = Netplay.singleton.players[currentCarrier];

            if (carryingPlayer)
            {
                bool shouldShowIndicator = carryingPlayer != Netplay.singleton.localPlayer;
                if (gotFlagIndicator.activeSelf != shouldShowIndicator)
                    gotFlagIndicator.SetActive(shouldShowIndicator);

                gotFlagIndicator.transform.SetPositionAndRotation(carryingPlayer.transform.position + Vector3.up * gotFlagIndicatorHoverHeight, Quaternion.LookRotation(gotFlagIndicator.transform.position - GameManager.singleton.camera.transform.position));
            }
        }
        else
        {
            if (gotFlagIndicator.activeSelf)
                gotFlagIndicator.SetActive(false);
        }
    }

    private void OnTriggerStay(Collider other) // using Stay because an invincible player may stand and wait til they can pick up the flag
    {
        if (NetworkServer.active && other.TryGetComponent(out Character character))
            ServerOnContinuousCollisionWithPlayer(character);
    }

    private void ServerOnContinuousCollisionWithPlayer(Character player)
    {
        if (NetworkServer.active && currentCarrier == -1 && Time.time - timeOfLastInteraction >= kInteractionCooldown
            && !player.damageable.isInvincible)
        {
            timeOfLastInteraction = Time.time;

            // pick up the flag
            if (player.team != team)
            {
                state = State.Carrying;
                currentCarrier = player.playerId;

                MessageFeed.Post($"<player>{player.playerName}</player> picked up the {team.ToColoredString()} flag!");
                RpcPlaySound(FlagSoundIndex.Pickup);
            }

            // return the slab
            // ...flag
            if (state == State.Dropped && player.team == team)
            {
                MessageFeed.Post($"<player>{player.playerName}</player> returned the {team.ToColoredString()} flag to base!");

                ReturnToBase(false);

                if (Netplay.singleton.localPlayer?.team == team)
                    RpcPlaySound(FlagSoundIndex.Returned);
            }
        }
    }

    public void Capture(Character player)
    {
        if (NetworkServer.active && MatchState.Get(out MatchTeams stateTeams) && MatchState.Get(out MatchFlags stateCtf))
        {
            MessageFeed.Post($"<player>{player.playerName}</player> captured the {team.ToColoredString()} flag!");

            stateTeams.AwardPoint(player.team);
            player.score += stateCtf.playerPointsPerCapture;
            player.holdingFlag.ReturnToBase(false);

            RpcPlaySound(FlagSoundIndex.Captured);
            timeOfLastInteraction = Time.time;
        }
    }

    public void ReturnToBase(bool postReturnMessage)
    {
        transform.position = basePosition;
        transform.rotation = baseRotation;

        state = State.Idle;

        dropRespawnCountdown = 0f;

        attachedToPlayer = -1;

        blinker.timeRemaining = 0f;

        if (NetworkServer.active)
        {
            if (postReturnMessage)
                MessageFeed.Post($"The {team.ToColoredString()} flag has returned to base.");

            currentCarrier = -1;
            syncMovement.SyncNow();

            RpcReturnToBase();
        }
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcReturnToBase()
    {
        if (Netplay.singleton.localPlayer && Netplay.singleton.localPlayer.team == team)
            GameSounds.PlaySound(null, returnedSound);

        blinker.timeRemaining = 0f; // stop a le blink
    }

    public void Drop()
    {
        if (NetworkServer.active)
        {
            if (currentCarrier != -1)
            {
                Character carryingPlayer = Netplay.singleton.players[currentCarrier];

                if (carryingPlayer != null)
                {
                    // begin movement
                    Vector2 dropDirection = Random.insideUnitCircle.normalized;

                    transform.position = carryingPlayer.transform.position;
                    transform.rotation = Quaternion.identity;
                    movement.velocity = new Vector3(dropDirection.x * dropHorizontalVelocity, dropVerticalVelocity, dropDirection.y * dropHorizontalVelocity);

                    // start countdown
                    dropRespawnCountdown = dropRespawnCountdownDuration;

                    // drop state
                    currentCarrier = -1;
                    attachedToPlayer = -1;
                    state = State.Dropped;

                    syncMovement.SyncNow();
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

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcDrop(float blinkTime)
    {
        blinker.timeRemaining = blinkTime;
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

    public bool ShouldBlockMovement(Movement source, in RaycastHit hit) => false; // we almost always want things to go through the flag

    public void OnMovementCollidedBy(Movement source, TickInfo tickInfo)
    {
        if (NetworkServer.active && source.TryGetComponent(out Character sourceCharacter))
        {
            ServerOnContinuousCollisionWithPlayer(sourceCharacter);
        }
    }
}
