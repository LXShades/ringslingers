using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ring : SyncedObject
{
    [Header("Ring")]
    [Tooltip("Speed that this ring spins at, in degrees per second")] public float spinSpeed = 180;
    public float hoverHeight = 0.375f;

    [Header("Dropped rings")]
    public bool isDroppedRing = false;

    public float pickupWarmupDuration = 0.75f;

    [Header("Hierarchy")]
    public GameObject pickupParticles;

    public GameSound pickupSound = new GameSound();

    // Components
    private RespawnableItem respawnableItem;

    private float awakeTime;

    public override void FrameAwake()
    {
        respawnableItem = GetComponent<RespawnableItem>();
        awakeTime = Frame.local.time;
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
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + new Vector3(0, spinSpeed * Frame.local.deltaTime, 0));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Player>() && (!isDroppedRing || Frame.local.time - awakeTime >= pickupWarmupDuration))
        {
            other.GetComponent<Player>().numRings++;
            pickupParticles.SetActive(true);
            pickupParticles.transform.SetParent(null);

            GameSounds.PlaySound(other.gameObject, pickupSound);

            if (!isDroppedRing)
                respawnableItem.Pickup();
            else
                Destroy(gameObject); // we aren't going to respawn (although rewinding stuff might need to... uh... ah we'll work it out)
        }
    }
}
