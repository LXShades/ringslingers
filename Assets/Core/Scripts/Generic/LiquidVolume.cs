using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiquidVolume : MonoBehaviour
{
    private static List<LiquidVolume> all = new List<LiquidVolume>();
    private static Dictionary<Collider, LiquidVolume> allCollidersToLiquidVolumes = new Dictionary<Collider, LiquidVolume>();

    private static Collider[] overlapBuffer = new Collider[48];

    private Collider collider;

    private void Awake()
    {
        collider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (collider == null)
            collider = GetComponent<Collider>(); // ??? do we need, does Awake call first?

        all.Add(this);
        allCollidersToLiquidVolumes.Add(collider, this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        allCollidersToLiquidVolumes.Remove(collider);
    }

    public static LiquidVolume GetContainingLiquid(Vector3 point)
    {
        int numOverlaps = Physics.OverlapSphereNonAlloc(point, 0f, overlapBuffer);

        for (int i = 0; i < numOverlaps; i++)
        {
            if (allCollidersToLiquidVolumes.TryGetValue(overlapBuffer[i], out LiquidVolume volume))
                return volume;
        }

        return null;
    }
}
