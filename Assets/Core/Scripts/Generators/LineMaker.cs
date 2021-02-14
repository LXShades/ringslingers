using UnityEditor;
using UnityEngine;

/// <summary>
/// An ObjectSpawner that spawns objects along a line
/// </summary>
[SelectionBase]
[ExecuteInEditMode]
public class LineMaker : ObjectSpawner
{
    [Tooltip("The direction and magnitude of the line")]
    public Vector3 line;

    [Tooltip("If enabled, a force of gravity is applied")]
    public bool useGravity = false;
    [Tooltip("When being launched along the direction of the line, this is the velocity along the line that the victim travels. Gravity is added during this")]
    public float gravityLaunchVelocity = 1f;
    [Tooltip("How much gravity to apply over time")]
    public float gravityForce = 9.8f;

    /// <summary>
    /// Places each object along the line. Called by ObjectSpawner
    /// </summary>
    /// <param name="obj">The current object to position</param>
    /// <param name="objIndex">The 0-based index of the object</param>
    public override void OnObjectUpdate(GameObject obj, int objIndex)
    {
        if (useGravity)
        {
            float distanceAlongLine = (objectSpacing * objIndex);
            float time = distanceAlongLine / gravityLaunchVelocity;
            Vector3 down = transform.InverseTransformDirection(Vector3.down);

            obj.transform.localPosition = line.normalized * distanceAlongLine + (down * gravityForce * time * time);
        }
        else
        {
            obj.transform.localPosition = line.normalized * (objectSpacing * objIndex);
        }
    }

    public override int GetNumObjects()
    {
        return (int)(transform.TransformVector(line).magnitude / objectSpacing);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LineMaker))]
[CanEditMultipleObjects]
public class LineMakerEditor : Editor
{
    private bool hasLinePositionChanged = false;

    void OnSceneGUI()
    {
        if (Event.current.type == EventType.MouseUp)
        {
            if (hasLinePositionChanged)
            {
                EditorUtility.SetDirty(target);
                hasLinePositionChanged = false;
            }
        }

        // Let the user move the end of the line
        LineMaker lineMaker = target as LineMaker;
        Vector3 globalLinePosition = lineMaker.transform.TransformPoint(lineMaker.line);

        EditorGUI.BeginChangeCheck();
        globalLinePosition = Handles.PositionHandle(globalLinePosition, Quaternion.identity);
            
        if (EditorGUI.EndChangeCheck())
        {
            if (!hasLinePositionChanged)
            {
                Undo.RecordObject(target, "Move line spawner endpoint");
                hasLinePositionChanged = true;
            }

            lineMaker.line = lineMaker.transform.InverseTransformPoint(globalLinePosition);
            lineMaker.RefreshChildren();
        }
    }
}
#endif