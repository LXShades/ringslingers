using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles automatically propagating level configuration changes to the level database
/// </summary>
public class LevelDatabaseAssetTracker : UnityEditor.AssetModificationProcessor
{
    public static string[] OnWillSaveAssets(string[] paths)
    {
        // todo check that it's the scene we're editing?
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
                    string[] contentDbs = AssetDatabase.FindAssets($"t:{nameof(RingslingersContentDatabase)}");

                    foreach (string dbGuid in contentDbs)
                    {
                        RingslingersContentDatabase contentDb = (RingslingersContentDatabase)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(dbGuid), typeof(RingslingersContentDatabase));

                        if (contentDb != null)
                        {
                            int sceneDbIndex = contentDb.content.levels.FindIndex(a => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(a.path).buildIndex == currentSceneIndex);

                            if (config.configuration.includeInMapSelection || config.configuration.includeInRotation)
                            {
                                RingslingersContent.Level asLevel = new RingslingersContent.Level()
                                {
                                    configuration = config.configuration,
                                    path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path
                                };

                                if (sceneDbIndex == -1)
                                {
                                    // add entry
                                    contentDb.InsertScene(asLevel);
                                }
                                else
                                {
                                    // update entry
                                    contentDb.UpdateScene(sceneDbIndex, asLevel);
                                }
                            }
                            else if (sceneDbIndex != -1)
                            {
                                // remove entry
                                contentDb.RemoveScene(sceneDbIndex);
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
