using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : WorldObjectComponent
{
    [Header("Player info")]
    /// <summary>
    /// Name of this player. By default, all players are Fred.
    /// </summary>
    public string playerName = "Fred";

    /// <summary>
    /// Player ID
    /// </summary>
    public int playerId;

    [Header("Player control")]
    /// <summary>
    /// Current inputs of this player
    /// </summary>
    public InputCmds input;

    /// <summary>
    /// Previous inputs of this player
    /// </summary>
    public InputCmds lastInput;

    public Vector3 remotePosition
    {
        get => _remotePosition;
        set
        { _remotePosition = value; if (playerId == 1) Debug.Log($"Remote position={value}"); }
    }

    private Vector3 _remotePosition;

    [Header("Shinies")]
    public int score = 0;

    /// <summary>
    /// Number of rings picked up
    /// </summary>
    public int numRings = 0;

    [Header("Ring drop")]
    public GameSound dropSound = new GameSound();

    public GameObject droppedRingPrefab;
    public Transform droppedRingSpawnPoint;

    public int maxDroppableRings = 20;

    public int ringDropLayers = 3;
    public float ringDropCloseVerticalVelocity = 3.8f;
    public float ringDropFarVerticalVelocity = 5;

    public float ringDropHorizontalVelocity = 10;

    [Header("Hurt")]
    public float hurtDefaultHorizontalKnockback = 5;
    public float hurtDefaultVerticalKnockback = 5;

    [Header("I-frames")]
    public float hitInvincibilityDuration = 1.5f;

    public float hitInvincibilityBlinkRate = 25f;
    private float invincibilityTimeRemaining;

    /// <summary>
    /// Forward vector representing where we're aiming
    /// </summary>
    public Vector3 aimForward;

    // Components
    /// <summary>
    /// Player movement component
    /// </summary>
    [HideInInspector] public CharacterMovement movement;

    public override void FrameAwake()
    {
        movement = GetComponent<CharacterMovement>();
    }

    public override void FrameUpdate(float deltaTime)
    {
        // Update aim
        float horizontalRads = input.horizontalAim * Mathf.Deg2Rad, verticalRads = input.verticalAim * Mathf.Deg2Rad;

        aimForward = new Vector3(Mathf.Sin(horizontalRads) * Mathf.Cos(verticalRads), -Mathf.Sin(verticalRads), Mathf.Cos(horizontalRads) * Mathf.Cos(verticalRads));

        // Invincibility blinky
        if (invincibilityTimeRemaining > 0)
        {
            invincibilityTimeRemaining = Mathf.Max(invincibilityTimeRemaining - deltaTime, 0);

            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer && invincibilityTimeRemaining > 0)
                renderer.enabled = ((int)(World.live.time * hitInvincibilityBlinkRate) & 1) == 0;
            else
                renderer.enabled = true; // we finished blinky blinkying
        }

        // Debug
        if (Input.GetKeyDown(KeyCode.R))
        {
            Hurt(null);
        }
    }

    public void Respawn()
    {
        GameObject[] spawners = GameObject.FindGameObjectsWithTag("PlayerSpawn");

        if (spawners.Length > 0)
        {
            GameObject spawnPoint = spawners[Random.Range(0, spawners.Length)];

            transform.position = spawnPoint.transform.position;
            transform.forward = spawnPoint.transform.forward.Horizontal(); // todo
        }
        else
        {
            Debug.LogWarning("No player spawners in this stage!");
        }
    }

    public void Hurt(GameObject instigator)
    {
        if (invincibilityTimeRemaining > 0 || movement.state.HasFlag(CharacterMovement.State.Pained))
            return; // can't touch this

        // Give score to the attacker if possible
        Player attackerAsPlayer = instigator.GetComponent<Player>();

        if (attackerAsPlayer)
            attackerAsPlayer.score += 50;

        movement.ApplyHitKnockback(-transform.forward.Horizontal() * hurtDefaultHorizontalKnockback + new Vector3(0, hurtDefaultVerticalKnockback, 0));

        DropRings();
    }

    public void StartInvincibilityTime()
    {
        invincibilityTimeRemaining = hitInvincibilityDuration;
    }

    public void DropRings()
    {
        int numToDrop = Mathf.Min(numRings, maxDroppableRings);
        int numDropped = 0;

        int numRingLayers = ringDropLayers;
        for (int ringLayer = 0; ringLayer < numRingLayers; ringLayer++)
        {
            float horizontalVelocity = (ringLayer + 1) * ringDropHorizontalVelocity / (numRingLayers);
            float verticalVelocity = Mathf.Lerp(ringDropCloseVerticalVelocity, ringDropFarVerticalVelocity, (float)(ringLayer + 1) / numRingLayers);

            // Inner ring
            int currentNumToDrop = (ringLayer < numRingLayers - 1 ? numToDrop / numRingLayers : numToDrop - numDropped);
            float angleOffset = currentNumToDrop > 0 ? Mathf.PI * 2f / currentNumToDrop * ringLayer / (float)numRingLayers : 0;

            for (int i = 0; i < currentNumToDrop; i++)
            {
                float horizontalAngle = i * Mathf.PI * 2f / Mathf.Max(currentNumToDrop - 1, 1) + angleOffset;
                Movement ringMovement = GameManager.SpawnObject(droppedRingPrefab, droppedRingSpawnPoint.position, Quaternion.identity).GetComponent<Movement>();

                Debug.Assert(ringMovement);
                ringMovement.velocity = new Vector3(Mathf.Sin(horizontalAngle) * horizontalVelocity, verticalVelocity, Mathf.Cos(horizontalAngle) * horizontalVelocity);
                numDropped++;
            }
        }

        if (numToDrop > 0)
            GameSounds.PlaySound(gameObject, dropSound);

        numRings = 0;
    }

    private static readonly string[] nameSuffices = { " and Knuckles", " Jr", " Sr", " Classic", " Modern", " Esquire", " Ph.d", " Squared" }; // ive done my best
    public void Rename(string newName)
    {
        string updatedName = newName;
        int currentSuffix = 0;

        while (System.Array.Exists(worldObject.world.players, a => a != null && a != this && a.playerName == updatedName))
        {
            if (currentSuffix < nameSuffices.Length)
                updatedName = newName + nameSuffices[currentSuffix++];
            else
                updatedName = newName + nameSuffices[currentSuffix / nameSuffices.Length] + nameSuffices[currentSuffix % nameSuffices.Length]; // crap lol itll be fine ok
        }

        playerName = updatedName;
    }
}
