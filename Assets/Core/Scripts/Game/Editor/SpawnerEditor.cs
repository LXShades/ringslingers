using Mirror;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Spawner))]
public class SpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        if (GUILayout.Button("Populate prefabs"))
        {
            Spawner spawner = target as Spawner;

            Undo.RecordObject(spawner, "Populating prefabs");
            spawner.spawnablePrefabs.Clear();

            foreach (string prefabGuid in AssetDatabase.FindAssets("t:prefab"))
            {
                UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(prefabGuid), typeof(GameObject));

                if (prefab && (prefab as GameObject).GetComponent<NetworkIdentity>() != null)
                {
                    spawner.spawnablePrefabs.Add(prefab as GameObject);
                }
            }

            EditorUtility.SetDirty(spawner.gameObject);
        }
    }
}
