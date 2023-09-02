using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class ModExporterEditorWindow : EditorWindow
{
    [System.Serializable]
    public class Settings
    {
        public List<string> modsPathsToExport = new List<string>();
        public List<string> allModsAvailable = new List<string>();
        public string lastExportPath = System.IO.Directory.GetCurrentDirectory();
    }

    private Settings settings;
    private const string statePrefKey = "ModExporterState";

    private Vector2 scrollPosition;

    [MenuItem("Ringslingers/Mod Exporter...")]
    public static void OpenModExporter()
    {
        ModExporterEditorWindow.Open();
    }

    public void Awake()
    {
        LoadState();
        RefreshList();
    }

    private void OnDestroy()
    {
        SaveState();
    }

    private void LoadState()
    {
        settings = JsonUtility.FromJson<Settings>(EditorPrefs.GetString(statePrefKey, "")) ?? new Settings();
    }

    private void SaveState()
    {
        EditorPrefs.SetString(statePrefKey, JsonUtility.ToJson(settings));
    }

    private void RefreshList()
    {
        // determine which mods exist
        settings.allModsAvailable.Clear();

        foreach (string mod in AssetDatabase.FindAssets($"t:{nameof(RingslingersContentDatabase)}"))
        {
            string modPath = AssetDatabase.GUIDToAssetPath(mod);

            settings.allModsAvailable.Add(modPath);
        }

        // remove mods that no longer exist
        for (int i = 0; i < settings.modsPathsToExport.Count; i++)
        {
            if (!settings.allModsAvailable.Contains(settings.modsPathsToExport[i]))
                settings.modsPathsToExport.RemoveAt(i--);
        }
    }

    private void OnGUI()
    {
        bool hasChangedSettings = false;

        GUILayout.Label("Ringslingers Mod Exporter", EditorStyles.boldLabel);
        GUILayout.Label("Welcome to the Mod Exporter! Ready to make a Mod?\n\n" +
            "It's recommended to store each mod you make in a folder.\n\n" +
            "Each mod folder you make requires a Ringslingers Content Database. You can make one under the Project tab by right-clicking in the mod folder -> Create -> Ringslingers Content Database\n\n" +
            "When you add new assets, edit the content database and ensure it's up to date. Use its tools to add content and give it the appropriate names, game modes, etc.\n\n" +
            "If all is well, it will appear in this list. When you're ready, tick it, export it, and give it a play! Have fun!\n", EditorStyles.wordWrappedLabel);

        GUILayout.Label("Mods to Export", EditorStyles.boldLabel);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition, EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        {
            GUILayout.BeginVertical();
            {
                foreach (string mod in settings.allModsAvailable)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(mod);
                    bool isAlreadyInList = settings.modsPathsToExport.Contains(mod);
                    if (GUILayout.Toggle(isAlreadyInList, name) != isAlreadyInList)
                    {
                        if (!isAlreadyInList)
                            settings.modsPathsToExport.Add(mod);
                        else
                            settings.modsPathsToExport.Remove(mod);

                        // select it in the inspector if the user clicks it
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(mod);
                    }
                }
            }
            GUILayout.EndVertical();
        }
        GUILayout.EndArea();

        if (GUILayout.Button("Refresh List", GUILayout.Height(40)))
        {
            RefreshList();
        }

        if (GUILayout.Button("Export...", GUILayout.Height(40)))
        {
            // set folder to export them into
            ExportSelectedMods();
        }

        if (hasChangedSettings)
            SaveState();
    }

    private void ExportSelectedMods()
    {
        string exportPath = EditorUtility.SaveFolderPanel("Export Mod", settings.lastExportPath, "");

        if (string.IsNullOrEmpty(exportPath))
            return;

        settings.lastExportPath = exportPath;

        // todo: character prefabs as well
        StringBuilder sb = new StringBuilder();
        List<AssetBundleBuild> bundleBuilds = new List<AssetBundleBuild>();

        foreach (var contentDatabasePath in settings.modsPathsToExport)
        {
            RingslingersContentDatabase contentDatabase = AssetDatabase.LoadAssetAtPath<RingslingersContentDatabase>(contentDatabasePath);

            if (contentDatabase != null)
            {
                if (contentDatabase.ScanForErrors(out string errors) == 0)
                {
                    // Add a bundle build for the levels if there are any
                    if (contentDatabase.content.levels.Count > 0 || contentDatabase.content.mapRotations.Count > 0)
                    {
                        HashSet<string> scenesToAdd = new HashSet<string>();
                        foreach (var level in contentDatabase.content.levels)
                            scenesToAdd.Add(level.path);

                        foreach (LevelRotation rotation in contentDatabase.content.mapRotations)
                        {
                            foreach (var level in rotation.levels)
                                scenesToAdd.Add(level.path);
                        }

                        bundleBuilds.Add(new AssetBundleBuild()
                        {
                            assetNames = scenesToAdd.ToArray(),
                            assetBundleName = $"{contentDatabase.name}.Scenes"
                        });
                    }

                    // Add a bundle build for the other assets and the content database itself
                    // For some rather frustrating reason, we can't put them in the same bundle with the scenes
                    HashSet<string> assetsToAdd = new HashSet<string>();
                    assetsToAdd.Add(AssetDatabase.GetAssetPath(contentDatabase)); // The content database should be the first asset added so we can find it upon load
                    foreach (var character in contentDatabase.content.characters)
                        assetsToAdd.Add(AssetDatabase.GetAssetPath(character.prefab));

                    bundleBuilds.Add(new AssetBundleBuild()
                    {
                        assetNames = assetsToAdd.ToArray(),
                        assetBundleName = $"{contentDatabase.name}.Assets"
                    });
                }
                else
                {
                    sb.AppendLine($"{contentDatabase.name} could not be exported due to errors:\n\n{errors}");
                }
            }
            else
            {
                sb.AppendLine($"{contentDatabasePath} could not be found");
            }
        }

        if (bundleBuilds.Count == 0)
        {
            EditorUtility.DisplayDialog("No mods found", "There were no valid mods to export", "OK :(");
            return;
        }

        BuildAssetBundlesParameters buildParams = new BuildAssetBundlesParameters()
        {
            bundleDefinitions = bundleBuilds.ToArray(),
            targetPlatform = EditorUserBuildSettings.activeBuildTarget,
            options = BuildAssetBundleOptions.None,
            subtarget = 0, // what is this?
            outputPath = exportPath
        };

        if (sb.Length > 0)
        {
            EditorUtility.DisplayDialog("Errors during export", $"Some exports failed with the following errors:\n\n{sb.ToString()}", "OK");
        }

        AssetBundleManifest manifest;
        try
        {
            manifest = BuildPipeline.BuildAssetBundles(buildParams);
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Errors during export", $"There was an error while building the asset bundle:\n\n{e.Message}", "OK :( :(");
            return;
        }

        if (manifest != null)
        {
#if UNITY_EDITOR_WIN
            Process.Start(exportPath);
#endif
            EditorUtility.DisplayDialog("Export complete!", $"Mods successfully exported!", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Errors during export", "There were unknown errors during the export. Please check the logs.", "OK");
        }
    }

    public static void Open()
    {
        EditorWindow.GetWindow<ModExporterEditorWindow>("Ringslingers Mod Exporter", true);
    }
}