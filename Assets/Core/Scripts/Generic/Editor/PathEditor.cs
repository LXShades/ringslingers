using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Path)), CanEditMultipleObjects]
public class PathEditor : Editor
{
    private int lastSelectedPoint = 0;

    public override void OnInspectorGUI()
    {
        Path path = (Path)target;
        base.OnInspectorGUI();

        if (GUILayout.Button("Insert point"))
        {
            Undo.RecordObject(path, "Insert path point");

            if (path.points.Count > lastSelectedPoint)
                path.points.Insert(lastSelectedPoint + 1, path.points[lastSelectedPoint] + Vector3.forward);
            else
                path.points.Add(path.points.Count > 0 ? path.points[path.points.Count - 1] + Vector3.forward : Vector3.zero);
        }

        if (GUILayout.Button("Remove selected"))
        {
            Undo.RecordObject(path, "Remove selected path point");

            if (lastSelectedPoint >= 0 && lastSelectedPoint < path.points.Count)
                path.points.RemoveAt(lastSelectedPoint);
        }
    }

    protected virtual void OnSceneGUI()
    {
        Path path = (Path)target;

        for (int i = 0; i < path.points.Count; i++)
        {
            Handles.zTest = i == lastSelectedPoint ? UnityEngine.Rendering.CompareFunction.Always : UnityEngine.Rendering.CompareFunction.LessEqual;

            Handles.color = Color.black;
            if (Handles.Button(path.transform.TransformPoint(path.points[i]), Quaternion.identity, 0.3f, 0.25f, Handles.DotHandleCap))
            {
                lastSelectedPoint = i;
            }

            Handles.color = Color.white;
            Handles.DrawLine(path.transform.TransformPoint(path.points[i]), path.transform.TransformPoint(path.points[(i + 1) % path.points.Count]));

            if (i == lastSelectedPoint)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPointPosition = Handles.PositionHandle(path.transform.TransformPoint(path.points[i]), Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(path, "Edit path point");
                    path.points[i] = path.transform.InverseTransformPoint(newPointPosition);
                }
            }
        }
    }
}