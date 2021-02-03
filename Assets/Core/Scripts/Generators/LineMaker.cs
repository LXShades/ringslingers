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

    /// <summary>
    /// Places each object along the line. Called by ObjectSpawner
    /// </summary>
    /// <param name="obj">The current object to position</param>
    /// <param name="objIndex">The 0-based index of the object</param>
    public override void OnObjectUpdate(GameObject obj, int objIndex)
    {
        obj.transform.localPosition = line.normalized * (objectSpacing * objIndex);
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