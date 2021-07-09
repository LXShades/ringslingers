using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AssetLookup))]
public class AssetLookupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        if (GUILayout.Button("Populate list"))
        {
            (target as AssetLookup).Populate();
        }
    }
}
