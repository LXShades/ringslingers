using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class RingslingersAssetManager
{
    public static bool shouldUseEditorAssetsInPlaymode
    {
        get => EditorPrefs.GetBool("shouldUseEditorAssetsInPlaymode", true);
        set => EditorPrefs.SetBool("shouldUseEditorAssetsInPlaymode", value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void OnInitPlaymode()
    {
        RingslingersCoreLoader.useEditorAssetsIfAvailable = shouldUseEditorAssetsInPlaymode;
    }

    [MenuItem("Ringslingers/Build Core AssetBundles", priority = 0)]
    public static void BuildCoreAssetBundles()
    {
        if (!System.IO.Directory.Exists(RingslingersCoreLoader.coreAssetsBuildPath))
            System.IO.Directory.CreateDirectory(RingslingersCoreLoader.coreAssetsBuildPath);

        AssetBundleManifest manifest = BuildAssetBundles(true, true, RingslingersCoreLoader.coreAssetsBuildPath, null);

        if (manifest != null)
            EditorUtility.DisplayDialog("Success", "Core assets built successfully", "OK");
        else
            EditorUtility.DisplayDialog("Failure", "Core asset build failed", "OK");
    }

    [MenuItem("Ringslingers/Build Core AssetBundles (exclude scenes)", priority = 1)]
    public static void BuildCoreAssetBundles_ExcludeScenes()
    {
        if (!System.IO.Directory.Exists(RingslingersCoreLoader.coreAssetsBuildPath))
            System.IO.Directory.CreateDirectory(RingslingersCoreLoader.coreAssetsBuildPath);

        AssetBundleManifest manifest = BuildAssetBundles(true, false, RingslingersCoreLoader.coreAssetsBuildPath, null);

        if (manifest != null)
            EditorUtility.DisplayDialog("Success", "Core assets built successfully", "OK");
        else
            EditorUtility.DisplayDialog("Failure", "Core asset build failed", "OK");
    }

    [MenuItem("Ringslingers/Open Core AssetBundle Build Folder", priority = 2)]
    public static void OpenAssetBundleFolder()
    {
        System.Diagnostics.Process.Start(RingslingersCoreLoader.coreAssetsBuildPath);
    }

    [MenuItem("Ringslingers/Use Editor Assets in Playmode (instead of AssetBundles)", priority = 30)]
    public static void UseEditorAssetsInPlaymode()
    {
        shouldUseEditorAssetsInPlaymode = !shouldUseEditorAssetsInPlaymode;

        if (shouldUseEditorAssetsInPlaymode)
            EditorUtility.DisplayDialog("In Unity, everything has a sacrifice", "When this is enabled, you don't need to Build Core AssetBundles to play the game in editor with your latest changes. However, you can't load mods during the session.", "I understand");
        else
            EditorUtility.DisplayDialog("In Unity, everything has a sacrifice", "When this is disabled, you can load mods in play mode. However, if you are editing core game content, you need to Build Core AssetBundles for the changes to take effect.", "I understand");
    }

    [MenuItem("Ringslingers/Use Editor Assets in Playmode (instead of AssetBundles)", validate = true)]
    private static bool UseEditorAssetsInPlaymode_Validate()
    {
        Menu.SetChecked("Ringslingers/Use Editor Assets in Playmode (instead of AssetBundles)", shouldUseEditorAssetsInPlaymode);
        return true;
    }

    /// <summary>
    /// Builds asset bundles for Ringslingers
    /// 
    /// Usually, you want includeCoreAssets to be true to ensure any added mods use those assets rather than duplicating them (which causes trouble)
    /// IncludeCoreScenes only needs to be true if you want to build the core scenes
    /// </summary>
    public static AssetBundleManifest BuildAssetBundles(bool includeCoreAssets, bool includeCoreScenes, string exportPath, string[] modContentDatabasePathsToExport)
    {
        StringBuilder sb = new StringBuilder();
        List<AssetBundleBuild> bundleBuilds = new List<AssetBundleBuild>();

        if (modContentDatabasePathsToExport != null)
        {
            // Add bundle build for all mods
            foreach (string contentDatabasePath in modContentDatabasePathsToExport)
            {
                RingslingersContentDatabase contentDatabase = AssetDatabase.LoadAssetAtPath<RingslingersContentDatabase>(contentDatabasePath);

                if (contentDatabase != null)
                {
                    if (contentDatabase.ScanForErrors(out string errors) == 0)
                    {
                        // Add a bundle build for the levels if there are any
                        if (contentDatabase.content.GetNumMaps() > 0)
                        {
                            HashSet<string> scenesToAdd = new HashSet<string>(contentDatabase.content.GetAllMaps().Select(x => x.path));

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
        }

        if (sb.Length > 0)
        {
            EditorUtility.DisplayDialog("Errors during export", $"Mod exports failed with the following errors:\n\n{sb.ToString()}", "OK");
            return null;
        }

        // We also need to add the core assets for proper referencing by the mod (do we need this?... yea probably)
        if (includeCoreAssets)
        {
            bundleBuilds.Add(new AssetBundleBuild()
            {
                assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(RingslingersCoreLoader.coreAssetBundleName),
                assetBundleName = RingslingersCoreLoader.coreAssetBundleName
            });
        }
        else
        {
            Debug.LogWarning("Building asset bundles without including core assets. Warning, this will result in duplication of any core assets referenced in the build scenes or prefabs, and may break things. This programmer ain't sure this should even be an option");
        }

        if (includeCoreScenes)
        {
            bundleBuilds.Add(new AssetBundleBuild()
            {
                assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(RingslingersCoreLoader.coreSceneBundleName),
                assetBundleName = RingslingersCoreLoader.coreSceneBundleName
            });
        }

        // Prepare to build
        BuildAssetBundlesParameters buildParams = new BuildAssetBundlesParameters()
        {
            bundleDefinitions = bundleBuilds.ToArray(),
            targetPlatform = EditorUserBuildSettings.activeBuildTarget,
            options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableLoadAssetByFileName | BuildAssetBundleOptions.DisableLoadAssetByFileNameWithExtension,
            outputPath = exportPath
        };

        AssetBundleManifest manifest;
        try
        {
            manifest = BuildPipeline.BuildAssetBundles(buildParams);
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Errors during export", $"There was an error while building the asset bundle:\n\n{e.Message}", "OK :( :(");
            return null;
        }

        if (manifest != null)
        {
            // Copy the bundles all builds in the build folder
            string buildFolder = System.IO.Path.GetFullPath(RingslingersCoreLoader.gameBuildPath);
            int numFilesCopied = 0;

            foreach (string buildDirectory in System.IO.Directory.EnumerateDirectories(buildFolder, "*", new System.IO.EnumerationOptions() { RecurseSubdirectories = true }))
            {
                string directoryName = System.IO.Path.GetFileName(buildDirectory);

                if (directoryName == $"{Application.productName}_Data")
                {
                    foreach (var bundleBuild in bundleBuilds)
                    {
                        System.IO.File.Copy($"{exportPath}/{bundleBuild.assetBundleName}", $"{buildDirectory}/{bundleBuild.assetBundleName}", true);
                    }
                }
            }

            if (numFilesCopied > 0)
                Debug.Log($"Copied asset bundle files to builds in Builds folder ({numFilesCopied} copies made)");
        }

        return manifest;
    }
}
