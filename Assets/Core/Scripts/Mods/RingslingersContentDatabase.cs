using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine.Serialization;
using System.Linq;
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

    [Header("Map Rotations")]
    public List<MapRotation> mapRotations = new List<MapRotation>();

    /// <summary>The mod containing this content, if applicable (null for built-in content)</summary>
    public RingslingersMod sourceMod;

    public static void LoadContent(RingslingersContent content)
    {
        loaded.characters.AddRange(content.characters);
        loaded.mapRotations.AddRange(content.mapRotations);
    }

    public IEnumerable<MapConfiguration> GetAllMaps()
    {
        foreach (MapRotation rotation in mapRotations)
        {
            foreach (MapConfiguration level in rotation.maps)
                yield return level;
        }
    }

    public int GetNumMaps()
    {
        int numMaps = 0;
        foreach (MapRotation rotation in mapRotations)
            numMaps += rotation.maps.Count;
        return numMaps;
    }
}

[CreateAssetMenu(fileName = "New Ringslingers Content Database", menuName = "Ringslingers Content Database")]
public class RingslingersContentDatabase : ScriptableObject
{
    public string defaultMapRotationName = "Default Rotation";

    public RingslingersContent content = new RingslingersContent();

#if UNITY_EDITOR
    public void RescanContent()
    {
        string[] searchFolders = new string[] { System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)) };

        // Scan and add scenes that aren't already in the database and add them to the Default map rotation
        HashSet<string> existingScenePaths = new HashSet<string>(content.GetAllMaps().Select(x => x.path));
        List<MapConfiguration> newFoundMaps = new List<MapConfiguration>();

        foreach (string sceneCandidateGuid in AssetDatabase.FindAssets("t:scene", searchFolders))
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneCandidateGuid);

            if (!existingScenePaths.Contains(scenePath))
            {
                newFoundMaps.Add(new MapConfiguration()
                {
                    friendlyName = TryMakeFriendlyNameForScene(scenePath),
                    path = scenePath
                });
            }
        }

        if (newFoundMaps.Count > 0)
        {
            MapRotation defaultRotation = content.mapRotations.Find(x => x.name == defaultMapRotationName);
            if (defaultRotation == null)
                content.mapRotations.Add(defaultRotation = new MapRotation() { name = defaultMapRotationName });

            defaultRotation.maps.AddRange(newFoundMaps);
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
            EditorUtility.DisplayDialog($"{numErrors} errors found", $"{errors}\n\nYou should correct these for your mod to run correctly.", "OK");

        // Apply changes
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    public int ScanForErrors(out string errorDescription)
    {
        StringBuilder sb = new StringBuilder();
        int numErrors = 0;

        foreach (MapConfiguration level in content.GetAllMaps())
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

        sceneName = Regex.Replace(sceneName, "[^_]+_(.*)", "$1");
        sceneName = Regex.Replace(sceneName, "_", " ");
        sceneName = Regex.Replace(sceneName, "([a-z])([A-Z]|[0-9])", "$1 $2");
        return sceneName;
    }

    private string TryMakeFriendlyNameForCharacter(string path)
    {
        return System.IO.Path.GetFileNameWithoutExtension(path).Replace("Character", "");
    }
#endif
}