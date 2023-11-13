using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiquidVolume : MonoBehaviour
{
    private static List<LiquidVolume> all = new List<LiquidVolume>();
    private static Dictionary<Collider, LiquidVolume> allCollidersToLiquidVolumes = new Dictionary<Collider, LiquidVolume>();

    private static Collider[] overlapBuffer = new Collider[48];

    private new Collider collider;

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

    /// <summary>
    /// Gets the liquid containing the point and the given radius.
    /// </summary>
    public static LiquidVolume GetContainingLiquid(Vector3 point, float radius = 0.001f)
    {
        if (radius < 0.001f)
            radius = 0.001f; // Radius of 0 can cause unexpected slowdown in some maps, such as Desolate Twilight and Thunder Citadel, not sure why.

        int numOverlaps = Physics.OverlapSphereNonAlloc(point, radius, overlapBuffer);

        for (int i = 0; i < numOverlaps; i++)
        {
            if (allCollidersToLiquidVolumes.TryGetValue(overlapBuffer[i], out LiquidVolume volume))
                return volume;
        }

        return null;
    }
}
