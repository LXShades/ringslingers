using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ring : WorldObjectComponent
{
    [Header("Ring")]
    [Tooltip("Speed that this ring spins at, in degrees per second")] public float spinSpeed = 180;
    public float hoverHeight = 0.375f;

    [Header("Dropped rings")]
    public bool isDroppedRing = false;

    public float pickupWarmupDuration = 0.75f;

    [Header("Hierarchy")]
    public GameObject pickupParticles;

    [WorldIgnoreReference] public GameSound pickupSound = new GameSound();

    // Components
    private RespawnableItem respawnableItem;

    public delegate void OnPickup(Player player);
    [WorldIgnoreReference] public OnPickup onPickup;

    private float awakeTime;

    public override void FrameAwake()
    {
        respawnableItem = GetComponent<RespawnableItem>();
        awakeTime = World.live.time;
    }

    public override void FrameStart()
    {
        // Hover above the ground
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -Vector3.up, out hit, hoverHeight, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + new Vector3(0, hoverHeight, 0);
        }
    }

    public override void FrameUpdate()
    {
        base.FrameUpdate();

        // Spinny spin
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(0, spinSpeed * World.live.deltaTime, 0));
    }

    private void OnTriggerStay(Collider other)
    {
        Player otherPlayer = other.GetComponent<Player>();
        if (otherPlayer && (!isDroppedRing || World.live.time - awakeTime >= pickupWarmupDuration))
        {
            otherPlayer.numRings++;
            pickupParticles.SetActive(true);
            pickupParticles.transform.SetParent(null);

            GameSounds.PlaySound(other.gameObject, pickupSound);

            if (!isDroppedRing)
                respawnableItem.Pickup();
            else
                GameManager.DestroyObject(gameObject); // we aren't going to respawn here

            onPickup?.Invoke(otherPlayer);
        }
    }
}
