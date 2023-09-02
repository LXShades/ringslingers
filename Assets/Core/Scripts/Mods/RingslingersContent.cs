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

    [Header("Levels (please do not edit manually, go to the scene instead)")]
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
        SortScenes();

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
        content.levels.Clear();

        List<Scene> scenesToUnload = new List<Scene>();

        foreach (var sceneSetting in EditorBuildSettings.scenes)
        {
            if (!sceneSetting.enabled)
                continue;

            string scenePath = sceneSetting.path;
            Scene loadedScene;

            if (!EditorSceneManager.GetSceneByPath(scenePath).IsValid())
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                loadedScene = EditorSceneManager.GetSceneByPath(scenePath);
                scenesToUnload.Add(loadedScene);
            }
            else
            {
                loadedScene = EditorSceneManager.GetSceneByPath(scenePath);
            }

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

        foreach (Scene scene in scenesToUnload)
            EditorSceneManager.CloseScene(scene, true);

        SortScenes();

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

    public void SortScenes()
    {
        content.levels.Sort((a, b) => SceneUtility.GetBuildIndexByScenePath(a.path) - SceneUtility.GetBuildIndexByScenePath(b.path));
    }
#endif
}