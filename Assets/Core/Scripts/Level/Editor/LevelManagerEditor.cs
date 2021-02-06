using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelDatabase))]
public class LevelManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Rescan Scenes from BuildSettings"))
        {
            (target as LevelDatabase).RescanScenes();
        }
    }
}
