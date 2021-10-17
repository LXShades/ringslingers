using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles automatically propagating level configuration changes to the level database
/// </summary>
public class LevelDatabaseAssetTracker : UnityEditor.AssetModificationProcessor
{
    public static string[] OnWillSaveAssets(string[] paths)
    {
        if (paths.Length == 1 && paths[0].EndsWith(".unity"))
        {
            UnityEngine.Debug.Log($"Updating level database with {paths[0]}");

            LevelConfigurationComponent config = Object.FindObjectOfType<LevelConfigurationComponent>();
            int currentSceneIndex = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().buildIndex;

            // todo: this assumes the scene being saved is the open one but maybe it isn't?
            if (config != null)
            {
                if (currentSceneIndex != -1)
                {
                    string[] levelDbs = AssetDatabase.FindAssets("t:LevelDatabase");

                    foreach (string dbGuid in levelDbs)
                    {
                        LevelDatabase levelDb = (LevelDatabase)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(dbGuid), typeof(LevelDatabase));

                        if (levelDb != null)
                        {
                            int sceneDbIndex = levelDb.levels.FindIndex(a => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(a.path).buildIndex == currentSceneIndex);

                            if (config.configuration.includeInMapSelection || config.configuration.includeInRotation)
                            {
                                LevelDatabase.Level asLevel = new LevelDatabase.Level()
                                {
                                    configuration = config.configuration,
                                    path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path
                                };

                                if (sceneDbIndex == -1)
                                {
                                    // add entry
                                    levelDb.InsertScene(asLevel);
                                }
                                else
                                {
                                    // update entry
                                    levelDb.UpdateScene(sceneDbIndex, asLevel);
                                }
                            }
                            else if (sceneDbIndex != -1)
                            {
                                // remove entry
                                levelDb.RemoveScene(sceneDbIndex);
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Level database not updated: current scene is not in build settings.");
                }
            }
        }

        return paths;
    }
}
