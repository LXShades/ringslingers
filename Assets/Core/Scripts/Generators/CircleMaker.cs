using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An ObjectSpawner that positions each object into a circle around the up axis
/// </summary>
[ExecuteInEditMode]
public class CircleMaker : ObjectSpawner {
    [Range(0.5f, 20)]
    [Tooltip("The radius of the circle to spawn the objects in")]
    public float circleRadius = 2.0f;

    /// <summary>
    /// Positions each object into the circle. Called when each object is created
    /// </summary>
    /// <param name="obj">Pointer to the object</param>
    /// <param name="objIndex">0-based index of the object</param>
    public override void OnObjectUpdate(GameObject obj, int objIndex)
    {
        float angleInterval = Mathf.PI * 2.0f / GetNumObjects();

        obj.transform.localPosition = new Vector3(Mathf.Sin(angleInterval * objIndex), 0.0f, Mathf.Cos(angleInterval * objIndex)) * circleRadius;
    }

    public override int GetNumObjects()
    {
        return (int)(circleRadius * 2 * Mathf.PI / objectSpacing);
    }
}
