using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[System.Serializable]
public class RingslingersContent
{
    [System.Serializable]
    public struct Level
    {
        /// path of the scene file
        public string path;

        // configuration associated with the scene
        [Header("Config - Please do not edit this here, edit it in the scene")]
        public LevelConfiguration configuration;
    }

    [System.Serializable]
    public struct Character
    {
        public GameObject prefab;

        public CharacterConfiguration configuration;
    }

    /// <summary>
    /// All currently loaded Ringslingers Content
    /// </summary>
    public static RingslingersContent loaded = new RingslingersContent();

    [Header("Levels")]
    public List<Level> levels = new List<Level>();

    [Header("Characters")]
    public List<Character> characters = new List<Character>();

    /// <summary>The mod containing this content, if applicable (null for built-in content)</summary>
    public RingslingersMod sourceMod;
}

[CreateAssetMenu(fileName = "New Ringslingers Content Database", menuName = "Ringslingers Content Database")]
public class RingslingersContentDatabase : ScriptableObject
{
    public RingslingersContent content = new RingslingersContent();

#if UNITY_EDITOR
    public void InsertScene(RingslingersContent.Level item, bool doSave = true)
    {
        content.levels.Add(item);

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void UpdateScene(int sceneIndex, RingslingersContent.Level item, bool doSave = true)
    {
        content.levels[sceneIndex] = item;

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void RemoveScene(int sceneIndex, bool doSave = true)
    {
        content.levels.RemoveAt(sceneIndex);

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void RescanContent()
    {
        // Scans all scenes for a LevelConfiguration and adds to the list
        Dictionary<string, int> rotationIndexByPath = new Dictionary<string, int>();

        for (int i = 0; i < content.levels.Count; i++)
            rotationIndexByPath.Add(content.levels[i].path, i);

        content.levels.Clear();

        List<Scene> scenesToUnload = new List<Scene>();
        const int maxNumScenesToOpenConcurrently = 10;

        // Scan and add scenes
        foreach (string sceneCandidateGuid in AssetDatabase.FindAssets("t:scene", new string[] { System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)) }))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneCandidateGuid);
            Scene loadedScene;

            if (!EditorSceneManager.GetSceneByPath(scenePath).IsValid())
            {
                // don't open too many scenes concurrently - close them all now if we've got too many open
                if (scenesToUnload.Count > maxNumScenesToOpenConcurrently)
                {
                    foreach (Scene sceneToUnload in scenesToUnload)
                        EditorSceneManager.CloseScene(sceneToUnload, true);

                    scenesToUnload.Clear();
                }

                // Open this scene
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                loadedScene = EditorSceneManager.GetSceneByPath(scenePath);
                scenesToUnload.Add(loadedScene);
            }
            else
            {
                loadedScene = EditorSceneManager.GetSceneByPath(scenePath);
            }

            // Add the scene's config into the levels list
            LevelConfiguration config = FindConfigurationInScene(loadedScene);

            if (config != null)
            {
                content.levels.Add(new RingslingersContent.Level()
                {
                    path = scenePath,
                    configuration = config
                });
            }
        }

        // Close remaining open scenes
        foreach (Scene scene in scenesToUnload)
            EditorSceneManager.CloseScene(scene, true);

        // Sort the map rotation
        content.levels.Sort((x, y) => (rotationIndexByPath.TryGetValue(x.path, out int xScore) ? xScore : 0) -
                                        (rotationIndexByPath.TryGetValue(y.path, out int yScore) ? yScore : 0));

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    private LevelConfiguration FindConfigurationInScene(Scene scene)
    {
        foreach (GameObject gameObject in scene.GetRootGameObjects())
        {
            LevelConfigurationComponent config = gameObject.GetComponentInChildren<LevelConfigurationComponent>();

            if (config != null)
                return config.configuration;
        }

        return null;
    }
#endif
}