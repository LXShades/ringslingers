using System.Collections.Generic;
using UnityEngine;

public class GravityVolume : MonoBehaviour
{
    //[Tooltip("Gravity influence towards the affected direction/centre, where 1 = the default gravity force in that direction")]
    //public float gravityMultiplier = 1f; // not used yet, might never get used

    [Tooltip("Radius where gravity is at 100% influence")]
    public float minRadius;
    [Tooltip("Radius where gravity loses influence. Actual gravity will blend between based on distance")]
    public float maxRadius;

    public static List<GravityVolume> instances = new List<GravityVolume>();

    private void OnEnable() => instances.Add(this);
    private void OnDisable() => instances.Remove(this);

    /// <summary>
    /// Returns all aggregated gravitational influences at this position. If there is no influence, gravityDirectionInOut is unchanged and false is returned
    /// </summary>
    public static bool GetInfluences(Vector3 positionIn, ref Vector3 gravityDirectionInOut)
    {
        bool hasFoundInfluence = false;

        foreach (GravityVolume gravVol in instances)
        {
            if (gravVol.GetInfluence(positionIn, ref gravityDirectionInOut))
                hasFoundInfluence = true;
        }

        return hasFoundInfluence;
    }

    /// <summary>
    /// Returns the gravitational influence at this position. If there is no influence, gravityDirectionInOut is unchanged and false is returned
    /// </summary>
    public bool GetInfluence(Vector3 positionIn, ref Vector3 gravityDirectionInOut)
    {
        float distance = Vector3.Distance(positionIn, transform.position);

        if (distance <= minRadius)
        {
            gravityDirectionInOut = (transform.position - positionIn).normalized;
            return true;
        }
        else if (distance <= maxRadius)
        {
            gravityDirectionInOut = Vector3.Slerp((transform.position - positionIn).normalized, Vector3.down, (distance - minRadius) / (maxRadius - minRadius));
            return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, minRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, maxRadius);
    }
}
