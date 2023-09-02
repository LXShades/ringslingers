using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RingslingersContentDatabase))]
public class RingslingersContentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Rescan Scenes from BuildSettings"))
        {
            (target as RingslingersContentDatabase).RescanContent();
        }
    }
}
