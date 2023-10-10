using Mirror;
using System.Collections.Generic;
using UnityEngine;

public class ItemActivatorVolume : MonoBehaviour
{
    private static List<ItemActivatorVolume> volumesToProcess = new List<ItemActivatorVolume>();

    public bool enabledByDefault = true;

    private List<RespawnableItem> affectedItems = new List<RespawnableItem>();

    private bool itemsAreCurrentlyEnabled;

    private void Awake()
    {
        itemsAreCurrentlyEnabled = enabledByDefault;
        volumesToProcess.Add(this);
    }

    private void Update()
    {
        // this is really a hack, we just don't really want every Awake() call iterating every affected item
        if (volumesToProcess.Count > 0)
        {
            Collider[] overlapTestResults = new Collider[16];

            foreach (RespawnableItem item in FindObjectsByType<RespawnableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int numHits = Physics.OverlapSphereNonAlloc(item.transform.position, 0.01f, overlapTestResults, (1 << gameObject.layer));

                for (int i = 0; i < numHits; i++)
                {
                    if (overlapTestResults[i].TryGetComponent(out ItemActivatorVolume itemActivatorVolume))
                    {
                        if (volumesToProcess.Contains(itemActivatorVolume))
                            itemActivatorVolume.affectedItems.Add(item);
                    }
                }
            }

            volumesToProcess.Clear();
        }

        if (NetworkServer.active)
        {
            foreach (var item in affectedItems)
            {
                if (!itemsAreCurrentlyEnabled)
                {
                    if (item.isSpawned)
                        item.DespawnForSeconds(1f);
                    else
                        item.isRespawnPaused = true;
                }
                else if (itemsAreCurrentlyEnabled)
                {
                    item.isRespawnPaused = false;
                }
            }
        }
    }

    public void SetItemsEnabled(bool enabled)
    {
        itemsAreCurrentlyEnabled = enabled;
    }
}
