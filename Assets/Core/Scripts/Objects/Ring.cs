using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ring : SyncedObject
{
    [Header("Ring")]
    [Tooltip("Speed that this ring spins at, in degrees per second")] public float spinSpeed = 180;
    public float hoverHeight = 0.375f;

    [Header("Hierarchy")]
    public GameObject pickupParticles;

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
        if (other.GetComponent<Player>())
        {
            other.GetComponent<Player>().numRings++;
            pickupParticles.SetActive(true);
            pickupParticles.transform.SetParent(null);
            Destroy(gameObject);
        }
    }
}
