using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
#endif

[CreateAssetMenu(fileName = "New Level Database", menuName = "Level Database")]
public class LevelDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Level
    {
        /// path of the scene file
        public string path;

        // configuration associated with the scene
        public LevelConfiguration configuration;
    }

    [Header("Levels (please do not edit manually, go to the scene instead)")]
    public List<Level> levels = new List<Level>();

#if UNITY_EDITOR
    public void InsertScene(Level item, bool doSave = true)
    {
        levels.Add(item);
        SortScenes();

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void UpdateScene(int sceneIndex, Level item, bool doSave = true)
    {
        levels[sceneIndex] = item;

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void RemoveScene(int sceneIndex, bool doSave = true)
    {
        levels.RemoveAt(sceneIndex);

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void RescanScenes()
    {
        // Scans all scenes for a LevelConfiguration and adds to the list
        levels.Clear();

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
                levels.Add(new Level()
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
        levels.Sort((a, b) => SceneUtility.GetBuildIndexByScenePath(a.path) - SceneUtility.GetBuildIndexByScenePath(b.path));
    }
#endif
}
