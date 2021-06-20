using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// An ObjectSpawner that spawns objects in an arc shape
/// </summary>
[ExecuteInEditMode]
public class ArcMaker : ObjectSpawner {
    [Tooltip("The number of degrees that this arc spans")]
    [Range(0, 360)]
    public float arcDegrees = 360f;

    [Tooltip("A point where the arc crosses")]
    public Vector3 arcTarget = new Vector3(1, 0, 0);

    [Tooltip("Spin on the arc axis")]
    public float arcRotation = 0f;

    /// <summary>
    /// Positions each object into the arc shape. Called by the ObjectSpawner
    /// </summary>
    /// <param name="obj">The object</param>
    /// <param name="objIndex">The index of the object</param>
    public override void OnObjectUpdate(GameObject obj, int objIndex)
    {
        // Places the object at a percentage derived from the object index
        int numObjects = GetNumObjects();
        float angleInterval = arcDegrees / Mathf.Max(numObjects - 1, 1);
        float angleStart = -angleInterval * (numObjects - 1) / 2.0f;
        Vector3 toArc = transform.TransformVector(arcTarget);

        obj.transform.position = transform.position + (Quaternion.AngleAxis(angleStart + angleInterval * objIndex, Quaternion.AngleAxis(arcRotation, toArc) * Vector3.Cross(toArc, Vector3.up)) * toArc);
    }

    public override int GetNumObjects()
    {
        return (int)(Mathf.PI * transform.TransformVector(arcTarget).magnitude * 2 * arcDegrees / 360.0f);
    }
}



#if UNITY_EDITOR
[CustomEditor(typeof(ArcMaker))]
[CanEditMultipleObjects]
public class ArcMakerEditor : Editor
{
    void OnSceneGUI()
    {
        // Let the user move the end of the line
        ArcMaker arcMaker = target as ArcMaker;
        Vector3 globalTarget = arcMaker.transform.TransformPoint(arcMaker.arcTarget);

        EditorGUI.BeginChangeCheck();
        globalTarget = Handles.PositionHandle(globalTarget, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Move arc spawner target");
            arcMaker.arcTarget = arcMaker.transform.InverseTransformPoint(globalTarget);
            arcMaker.RefreshChildren();
        }
    }
}
#endif