using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Carryable : NetworkBehaviour, IMovementCollisionCallbacks
{
    private static Dictionary<int, List<Carryable>> carryablesByPlayerIndex = new Dictionary<int, List<Carryable>>();

    public delegate bool OnAttemptPickupDelegate(Character character);

    public enum State
    {
        Idle,
        Dropped,
        Carrying
    }

    public enum CarrySlot
    {
        Flag,
        Shard,
    }

    [Header("Limits")]
    public CarrySlot carrySlot;
    public int carryLimitForSlot = 2;

    [Header("Visuals")]
    public Transform handCarrySocket;
    public Vector3 localHandCarrySocketOffset;

    [Header("Drop")]
    public float dropInteractionCooldown = 0.2f; // basically needed due to physics system quirks and collisions
    public float dropExpiryDuration = 15f;
    public bool dropUpdateBlinkerIfAvailable = true; // if there's a Blinker component, sync it up with the drop time until we expire/respawn

    // State
    [SyncVar]
    private State _state = State.Idle;

    public State state
    {
        get => _state;
        private set => _state = value;
    }

    private int currentCarrierDictionaryIdx = -1;

    // Carrying state
    [SyncVar(hook = nameof(OnCurrentCarrierChanged), callHookOnServer = true)]
    private int _currentCarrier = -1;

    public int currentCarrier { get => _currentCarrier; set => _currentCarrier = value; }
    public Character currentCarrierCharacter => _currentCarrier != -1 && currentCarrier < Netplay.singleton.players.Count ? Netplay.singleton.players[currentCarrier] : null;

    public bool isHiddenDueToFirstPersonCarrying { get; private set; }

    // Idle/dropped state
    private float timeOfEndCooldown = -1f;
    private float timeOfDropExpiry = -1f;

    public OnAttemptPickupDelegate onAttemptPickupServer;
    public Action onDropExpiredServer;
    public Action<Character> onDropped;
    public Action onLostPlayer;
    public Action onCarryStateChanged;

    private List<Renderer> renderers;
    private Visibility visibility;
    private Blinker blinker;

    private static List<Carryable> emptyList = new List<Carryable>();

    private void Awake()
    {
        renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
        blinker = GetComponent<Blinker>();
        visibility = GetComponent<Visibility>();

        localHandCarrySocketOffset = handCarrySocket ? Quaternion.Inverse(transform.rotation) * (handCarrySocket.position - transform.position) : Vector3.zero;
    }

    private void Update()
    {
        // Allow recovery if we somehow lost our character
        if (currentCarrierCharacter == null && currentCarrier != -1)
        {
            if (NetworkServer.active)
                ServerDetachFromPlayer();
            onLostPlayer?.Invoke();
        }

        // Drop "expires" after left too long
        if (state == State.Dropped && NetworkServer.active && Time.time - Time.deltaTime < timeOfDropExpiry && Time.time >= timeOfDropExpiry)
        {
            if (blinker && dropUpdateBlinkerIfAvailable)
                blinker.timeRemaining = 0f;
            onDropExpiredServer?.Invoke();
        }

        // Hide when carrying in first-person
        if (PlayerCamera.singleton.isFirstPerson && PlayerCamera.singleton.currentPlayer != null && currentCarrier == PlayerCamera.singleton.currentPlayer.playerId)
        {
            isHiddenDueToFirstPersonCarrying = true;

            if (blinker)
                blinker.Stop();
        }
        else
            isHiddenDueToFirstPersonCarrying = false;

        // Update visibility from first-personness
        if (visibility)
        {
            if (isHiddenDueToFirstPersonCarrying)
                visibility.Set(this, false);
            else
                visibility.Unset(this);
        }
    }

    private void OnDestroy()
    {
        currentCarrier = -1;
        UpdateCarrierDictionary();
    }

    private void OnTriggerStay(Collider other) // using Stay because an invincible player may stand and wait til they can pick up the flag
    {
        if (NetworkServer.active && other.TryGetComponent(out Character character))
            ServerOnContinuousCollisionWithPlayer(character);
    }

    public void ServerDetachFromPlayer()
    {
        currentCarrier = -1;
        if (NetworkServer.active)
            state = State.Dropped;

        LocalDetachFromPlayer();
    }

    private void LocalDetachFromPlayer()
    {
        UpdateCarrierDictionary();

        if (dropExpiryDuration > 0f)
        {
            timeOfDropExpiry = Time.time + dropExpiryDuration;
            if (blinker && dropUpdateBlinkerIfAvailable)
                blinker.timeRemaining = dropExpiryDuration;
        }
    }

    private void ServerOnContinuousCollisionWithPlayer(Character player)
    {
        if (NetworkServer.active && currentCarrier == -1 && Time.time > timeOfEndCooldown && !player.damageable.isInvincible)
        {
            // check we haven't reached the limit yet
            List<Carryable> allCarriedByPlayer = Carryable.GetAllCarriedByPlayer(player);
            int numOtherItemsInSlot = 0;

            foreach (var item in allCarriedByPlayer)
            {
                if (item.carrySlot == carrySlot)
                    numOtherItemsInSlot++;
            }

            if (carryLimitForSlot <= 0 || numOtherItemsInSlot < carryLimitForSlot)
            {
                StartInteractionCooldown(dropInteractionCooldown);

                if (onAttemptPickupServer == null || onAttemptPickupServer.Invoke(player))
                {
                    state = State.Carrying;
                    currentCarrier = player.playerId;
                }
            }
        }
    }

    public void ServerSetState(State state)
    {
        this.state = state;

        if (state == State.Idle)
            timeOfDropExpiry = -1f;
    }

    public void StartInteractionCooldown(float cooldownTime)
    {
        timeOfEndCooldown = Time.time + cooldownTime;
    }

    public void Drop()
    {
        if (NetworkServer.active)
        {
            Character carryingPlayer = currentCarrierCharacter;

            if (carryingPlayer != null)
            {
                // drop position
                transform.position = carryingPlayer.transform.position;
                transform.rotation = Quaternion.identity;
            }

            // drop state
            ServerDetachFromPlayer();

            // callbacks
            onDropped?.Invoke(carryingPlayer);
            RpcDrop(carryingPlayer);
        }
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcDrop(Character character)
    {
        if (!NetworkServer.active)
        {
            onDropped?.Invoke(character);

            // todo use syncvar instead for timer?
            LocalDetachFromPlayer();
        }
    }

    private void OnCurrentCarrierChanged(int previous, int next)
    {
        UpdateCarrierDictionary();

        // update blinker
        if (dropUpdateBlinkerIfAvailable && blinker && next != -1)
            blinker.Stop();
    }

    /// <summary>
    /// NOTE: if you drop carryables while iterating this, the collection will be modified during iteration and cause problems
    /// </summary>
    public static List<Carryable> GetAllCarriedByPlayer(Character playerCharacter)
    {
        if (playerCharacter == null)
            return emptyList;

        if (carryablesByPlayerIndex.TryGetValue(playerCharacter.playerId, out List<Carryable> output))
            return output;
        else
            return emptyList;
    }

    private void UpdateCarrierDictionary()
    {
        if (currentCarrierDictionaryIdx != -1)
            carryablesByPlayerIndex[currentCarrierDictionaryIdx].Remove(this);

        if (currentCarrier != -1)
        {
            List<Carryable> carriedByPlayer;
            if (!carryablesByPlayerIndex.TryGetValue(currentCarrier, out carriedByPlayer))
                carryablesByPlayerIndex[currentCarrier] = carriedByPlayer = new List<Carryable>();

            carriedByPlayer.Add(this);
        }
        currentCarrierDictionaryIdx = currentCarrier;
    }

    public bool ShouldBlockMovement(Movement source, in RaycastHit hit) => false; // we almost always want things to go through a carryable

    public void OnMovementCollidedBy(Movement source, TickInfo tickInfo)
    {
        if (NetworkServer.active && source.TryGetComponent(out Character sourceCharacter))
            ServerOnContinuousCollisionWithPlayer(sourceCharacter);
    }
}
