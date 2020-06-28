using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : SyncedObject
{
    [Header("Player info")]
    /// <summary>
    /// Name of this player. By default, all players are Fred.
    /// </summary>
    public new string name = "Fred";

    /// <summary>
    /// Player ID
    /// </summary>
    public int playerId;

    /// <summary>
    /// Associated Client ID
    /// </summary>
    public ulong clientId;

    [Header("Player control")]
    /// <summary>
    /// Current inputs of this player
    /// </summary>
    [HideInInspector] public InputCmds input;

    /// <summary>
    /// Previous inputs of this player
    /// </summary>
    [HideInInspector] public InputCmds lastInput;

    /// <summary>
    /// Number of rings picked up
    /// </summary>
    [HideInInspector] public int numRings = 0;

    [Header("Ring drop")]
    public GameObject droppedRingPrefab;

    public int maxDroppableRings = 20;

    public float ringDropCloseVerticalVelocity = 3.8f;
    public float ringDropFarVerticalVelocity = 5;

    public float ringDropHorizontalVelocity = 10;

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

    public override void FrameUpdate()
    {
        float horizontalRads = input.horizontalAim * Mathf.Deg2Rad, verticalRads = input.verticalAim * Mathf.Deg2Rad;

        aimForward = new Vector3(Mathf.Sin(horizontalRads) * Mathf.Cos(verticalRads), -Mathf.Sin(verticalRads), Mathf.Cos(horizontalRads) * Mathf.Cos(verticalRads));

        if (Input.GetKeyDown(KeyCode.R))
        {
            DropRings();
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

            // meh...charactercontroller bugs
            Physics.SyncTransforms();
        }
        else
        {
            Debug.LogWarning("No player spawners in this stage!");
        }
    }

    public void DropRings()
    {
        int numToDrop = Mathf.Min(numRings, maxDroppableRings);
        int numDropped = 0;

        numToDrop = 20;

        int numRingLayers = 2;
        for (int ringLayer = 0; ringLayer < numRingLayers; ringLayer++)
        {
            float horizontalVelocity = (ringLayer + 1) * ringDropHorizontalVelocity / (numRingLayers);
            float verticalVelocity = Mathf.Lerp(ringDropCloseVerticalVelocity, ringDropFarVerticalVelocity, (float)(ringLayer + 1) / numRingLayers);

            Debug.Log($"vert velo: {verticalVelocity}");

            // Inner ring
            int currentNumToDrop = (ringLayer < numRingLayers - 1 ? numToDrop / numRingLayers : numToDrop - numDropped);

            for (int i = 0; i < currentNumToDrop; i++)
            {
                float horizontalAngle = i * Mathf.PI * 2f / Mathf.Max(currentNumToDrop - 1, 1);
                Movement ringMovement = Instantiate(droppedRingPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity).GetComponent<Movement>();

                Debug.Assert(ringMovement);
                ringMovement.velocity = new Vector3(Mathf.Sin(horizontalAngle) * horizontalVelocity, verticalVelocity, Mathf.Cos(horizontalAngle) * horizontalVelocity);
                numDropped++;
            }
        }
    }
}
