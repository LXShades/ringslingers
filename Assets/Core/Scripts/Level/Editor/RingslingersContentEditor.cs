using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RingslingersContentDatabase))]
public class RingslingersContentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Rescan Content"))
        {
            (target as RingslingersContentDatabase).RescanContent();
        }

        if (GUILayout.Button("Check for Errors"))
        {
            int numErrors = (target as RingslingersContentDatabase).ScanForErrors(out string errors);
            if (numErrors > 0)
            {
                EditorUtility.DisplayDialog($"{numErrors} errors found", errors, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No errors found", "No errors found.", "OK");
            }
        }
    }
}
