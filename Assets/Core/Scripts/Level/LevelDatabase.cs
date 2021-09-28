using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
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
    public Level[] levels;

#if UNITY_EDITOR
    public void RescanScenes()
    {
        // Scans all scenes for a LevelConfiguration and adds to the list
        List<Level> levelList = new List<Level>();
        List<UnityEngine.SceneManagement.Scene> scenesToUnload = new List<UnityEngine.SceneManagement.Scene>();

        foreach (var sceneSetting in EditorBuildSettings.scenes)
        {
            if (!sceneSetting.enabled)
                continue;

            string scenePath = sceneSetting.path;
            UnityEngine.SceneManagement.Scene loadedScene;

            if (!UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(scenePath).IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                loadedScene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(scenePath);
                scenesToUnload.Add(loadedScene);
            }
            else
            {
                loadedScene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(scenePath);
            }

            LevelConfiguration config = FindConfigurationInScene(loadedScene);

            if (config != null)
            {
                levelList.Add(new Level()
                {
                    path = scenePath,
                    configuration = config
                });
            }
        }

        foreach (UnityEngine.SceneManagement.Scene scene in scenesToUnload)
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }

        levels = levelList.ToArray();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    private LevelConfiguration FindConfigurationInScene(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject gameObject in scene.GetRootGameObjects())
        {
            LevelConfigurationComponent config = gameObject.GetComponentInChildren<LevelConfigurationComponent>();

            if (config != null)
            {
                return config.configuration;
            }
        }

        return null;
    }
#endif
}
