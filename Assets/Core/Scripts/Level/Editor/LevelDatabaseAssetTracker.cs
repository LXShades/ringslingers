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

            if (config != null)
            {
                if (currentSceneIndex != -1)
                {
                    string[] levelDbs = AssetDatabase.FindAssets("t:LevelDatabase");

                    foreach (string dbGuid in levelDbs)
                    {
                        LevelDatabase levelDb = (LevelDatabase)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(dbGuid), typeof(LevelDatabase));
                        bool wasChangeMade = false;

                        if (levelDb != null)
                        {
                            for (int i = 0; i < levelDb.levels.Length; i++)
                            {
                                if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(levelDb.levels[i].path) == currentSceneIndex)
                                {
                                    levelDb.levels[i].configuration = config.configuration;
                                    wasChangeMade = true;
                                }
                            }
                        }

                        if (!wasChangeMade && (config.configuration.includeInMapSelection || config.configuration.includeInRotation))
                        {
                            if (EditorUtility.DisplayDialog("Save to level database?", $"This level's level configuration is marked with Include (in Rotation or Map Selection) but it is not in the level DB \"{levelDb.name}\". Would you like to add it to the level DB?", "Yes", "No"))
                            {
                                LevelDatabase.Level[] newLevels = new LevelDatabase.Level[levelDb.levels.Length + 1];

                                levelDb.levels.CopyTo(newLevels, 0);
                                newLevels[newLevels.Length - 1] = new LevelDatabase.Level() { configuration = config.configuration, path = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path };
                                levelDb.levels = newLevels;

                                wasChangeMade = true;
                            }
                        }

                        if (wasChangeMade)
                        {
                            EditorUtility.SetDirty(levelDb);
                            AssetDatabase.SaveAssets();
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
