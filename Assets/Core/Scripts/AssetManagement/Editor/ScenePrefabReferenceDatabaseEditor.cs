using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ScenePrefabReferenceDatabase))]
public class ScenePrefabReferenceDatabaseEditor : Editor
{
    [InitializeOnLoadMethod]
    private static void OnInit()
    {
        EditorSceneManager.sceneSaving += PreSceneSave;
    }

    private static void PreSceneSave(Scene scene, string path)
    {
        // Regenerate all scene prefab reference databases in the scene if we can (there should only be one really)
        foreach (GameObject obj in scene.GetRootGameObjects())
        {
            ScenePrefabReferenceDatabase prefabReferenceDatabase = obj.GetComponentInChildren<ScenePrefabReferenceDatabase>();
            if (prefabReferenceDatabase)
                Regenerate(prefabReferenceDatabase);
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Regenerate"))
        {
            Regenerate(target as ScenePrefabReferenceDatabase);
        }
    }

    public static void Regenerate(ScenePrefabReferenceDatabase targetDatabase)
    {
        List<ScenePrefabReferenceDatabase.ScenePrefabInstance> prefabInstances = new List<ScenePrefabReferenceDatabase.ScenePrefabInstance>();
        List<GUID> prefabGuidsById = new List<GUID>();

        Dictionary<GameObject, int> prefabReferenceToId = new Dictionary<GameObject, int>();

        // Find all prefab roots
        HashSet<GameObject> prefabRootInstances = new HashSet<GameObject>();
        foreach (GameObject gameObject in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (gameObject == PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject))
            {
                GameObject prefabType = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);

                if (prefabType != null)
                {
                    if (!prefabReferenceToId.ContainsKey(prefabType))
                    {
                        prefabReferenceToId.Add(prefabType, prefabGuidsById.Count);
                        prefabGuidsById.Add(AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(prefabType)));
                    }

                    prefabInstances.Add(new ScenePrefabReferenceDatabase.ScenePrefabInstance()
                    {
                        Object = gameObject,
                        PrefabId = prefabReferenceToId[prefabType]
                    });
                }
            }
        }

        // todo: test direct references
        targetDatabase.prefabGuidsById = prefabGuidsById.Select(x => x.ToString()).ToArray();
        targetDatabase.prefabInstances = prefabInstances.ToArray();
        targetDatabase.prefabReferencesById = prefabReferenceToId.Keys.ToArray();

        EditorUtility.SetDirty(targetDatabase);
    }
}
