using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[System.Serializable]
public class RingslingersContent
{
    /// <summary>
    /// All currently loaded Ringslingers Content
    /// </summary>
    public static RingslingersContent loaded = new RingslingersContent();

    [Header("Characters")]
    public List<CharacterConfiguration> characters = new List<CharacterConfiguration>();

    [Header("Levels")]
    public List<LevelConfiguration> levels = new List<LevelConfiguration>();

    [Header("Map Rotations")]
    public List<LevelRotation> mapRotations = new List<LevelRotation>();

    /// <summary>The mod containing this content, if applicable (null for built-in content)</summary>
    public RingslingersMod sourceMod;

    public static void LoadContent(RingslingersContent content)
    {
        loaded.levels.AddRange(content.levels);
        loaded.characters.AddRange(content.characters);
        loaded.mapRotations.AddRange(content.mapRotations);
    }
}

[CreateAssetMenu(fileName = "New Ringslingers Content Database", menuName = "Ringslingers Content Database")]
public class RingslingersContentDatabase : ScriptableObject
{
    public RingslingersContent content = new RingslingersContent();

#if UNITY_EDITOR
    public void InsertScene(LevelConfiguration item, bool doSave = true)
    {
        content.levels.Add(item);

        if (doSave)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void UpdateScene(int sceneIndex, LevelConfiguration item, bool doSave = true)
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
        string[] searchFolders = new string[] { System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)) };

        // Scan and add scenes that aren't already in the list
        foreach (string sceneCandidateGuid in AssetDatabase.FindAssets("t:scene", searchFolders))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneCandidateGuid);

            if (!content.levels.Exists(x => x.path == scenePath))
            {
                content.levels.Add(new LevelConfiguration()
                {
                    friendlyName = TryMakeFriendlyNameForScene(scenePath),
                    path = scenePath
                });
            }
        }

        // Then characters
        foreach (string characterCandidateGuid in AssetDatabase.FindAssets("t:prefab", searchFolders))
        {
            string characterPath = AssetDatabase.GUIDToAssetPath(characterCandidateGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPath);

            if (prefab != null)
            {
                if (!content.characters.Exists(x => x.prefab == prefab) && prefab.GetComponent<Character>() != null)
                {
                    content.characters.Add(new CharacterConfiguration()
                    {
                        name = TryMakeFriendlyNameForCharacter(characterPath),
                        prefab = prefab
                    });
                }
            }
        }

        // Run the validator to notify the user of issues
        int numErrors = ScanForErrors(out string errors);
        if (numErrors > 0)
        {
            EditorUtility.DisplayDialog($"{numErrors} errors found", $"{errors}\n\nYou should correct these for your mod to run correctly.", "OK");
        }

        // Apply changes
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    public int ScanForErrors(out string errorDescription)
    {
        StringBuilder sb = new StringBuilder();
        int numErrors = 0;

        List<LevelConfiguration> allLevels = new List<LevelConfiguration>(content.levels);
        foreach (LevelRotation rotation in content.mapRotations)
            allLevels.AddRange(rotation.levels);

        foreach (LevelConfiguration level in allLevels)
        {
            if (!AssetDatabase.AssetPathExists(level.path))
            {
                sb.AppendLine($"{level.friendlyName} points to a scene that does not exist (\"{level.path}\" was not found)");
                numErrors++;
            }

            if (level.defaultGameModePrefab == null)
            {
                sb.AppendLine($"{level.friendlyName} does not have a valid Game Mode set.");
                numErrors++;
            }

            if (level.defaultPlayerLimit <= 0)
            {
                sb.AppendLine($"{level.friendlyName} has an invalid player limit ({level.defaultPlayerLimit})");
                numErrors++;
            }

            if (level.maxRotationPlayers - level.minRotationPlayers <= 0)
            {
                sb.AppendLine($"{level.friendlyName} has an invalid range of max/min players to appear in the rotation. (Max={level.maxRotationPlayers} Min={level.minRotationPlayers}");
                numErrors++;
            }
        }

        foreach (CharacterConfiguration character in content.characters)
        {
            if (character.prefab == null)
            {
                sb.AppendLine($"Character {character.name} has an invalid prefab");
                numErrors++;
            }
        }

        errorDescription = sb.ToString();
        return numErrors;
    }

    private string TryMakeFriendlyNameForScene(string path)
    {
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);

        sceneName = Regex.Replace(sceneName, "[^_]+_(.*)", "$0");
        sceneName = Regex.Replace(sceneName, "_", " ");
        sceneName = Regex.Replace(sceneName, "([a-z])([A-Z])", "$0 $1");
        return sceneName;
    }

    private string TryMakeFriendlyNameForCharacter(string path)
    {
        return System.IO.Path.GetFileNameWithoutExtension(path).Replace("Character", "");
    }
#endif
}