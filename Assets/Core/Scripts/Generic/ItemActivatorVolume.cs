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
            // TODO: proper override of respawn behaviour, these items are respawning constantly and we just keep shoving them back into despawned land
            // TODO: item respawn time is overridden, can respawn instantly if returning shard
            foreach (var item in affectedItems)
            {
                if (item.isSpawned && !itemsAreCurrentlyEnabled)
                    item.Despawn();
                else if (!item.isSpawned && itemsAreCurrentlyEnabled)
                    item.Respawn();
            }
        }
    }

    public void SetItemsEnabled(bool enabled)
    {
        itemsAreCurrentlyEnabled = enabled;
    }
}
