using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class RingslingersAssetManager
{
    [Flags]
    public enum ModExportCopyFlags
    {
        None = 0,
        CopyToBuildCoreFolder = 1,
        CopyToBuildModsFolder = 2,
        CopyToAdditionalCopyPath = 4
    }

    public static bool shouldUseEditorAssetsInPlaymode
    {
        get => EditorPrefs.GetBool("shouldUseEditorAssetsInPlaymode", true);
        set => EditorPrefs.SetBool("shouldUseEditorAssetsInPlaymode", value);
    }
    public static bool shouldBuildModlessVersionForPlaytests
    {
        get => EditorPrefs.GetBool("shouldBuildModlessVersionForPlaytests", true);
        set => EditorPrefs.SetBool("shouldBuildModlessVersionForPlaytests", value);
    }

    [InitializeOnLoadMethod()]
    private static void OnInitEditMode()
    {
        PlaytestTools.onPreBuild += OnPreBuild;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void OnInitPlaymode()
    {
        RingslingersCoreLoader.useEditorAssetsIfAvailable = shouldUseEditorAssetsInPlaymode;
    }

    [MenuItem("Ringslingers/Build Core AssetBundles", priority = 0)]
    public static void BuildCoreAssetBundles()
    {
        AssetBundleManifest manifest = BuildAssetBundles(true, true, RingslingersCoreLoader.assetBundleBuildDirectory, null, ModExportCopyFlags.CopyToBuildCoreFolder);

        if (manifest != null)
            EditorUtility.DisplayDialog("Success", "Core assets built successfully", "OK");
        else
            EditorUtility.DisplayDialog("Failure", "Core asset build failed", "OK");
    }

    [MenuItem("Ringslingers/Build Core AssetBundles (exclude scenes)", priority = 1)]
    public static void BuildCoreAssetBundles_ExcludeScenes()
    {
        AssetBundleManifest manifest = BuildAssetBundles(true, false, RingslingersCoreLoader.assetBundleBuildDirectory, null, ModExportCopyFlags.CopyToBuildCoreFolder);

        if (manifest != null)
            EditorUtility.DisplayDialog("Success", "Core assets built successfully", "OK");
        else
            EditorUtility.DisplayDialog("Failure", "Core asset build failed", "OK");
    }

    [MenuItem("Ringslingers/Open Core AssetBundle Build Folder", priority = 2)]
    public static void OpenAssetBundleFolder()
    {
        System.Diagnostics.Process.Start(RingslingersCoreLoader.assetBundleBuildDirectory);
    }

    [MenuItem("Ringslingers/Disable AssetBundles in Playmode (disables mods in editor)", priority = 30)]
    public static void UseEditorAssetsInPlaymode()
    {
        shouldUseEditorAssetsInPlaymode = !shouldUseEditorAssetsInPlaymode;

        if (shouldUseEditorAssetsInPlaymode)
            EditorUtility.DisplayDialog("In Unity, everything has a sacrifice", "When this is enabled, you don't need to Build Core AssetBundles to play the game in editor with your latest changes. However, you can't load mods during the session.", "I understand");
        else
            EditorUtility.DisplayDialog("In Unity, everything has a sacrifice", "When this is disabled, you can load mods in play mode. However, if you are editing core game content, you need to Build Core AssetBundles for the changes to take effect.", "I understand");
    }

    [MenuItem("Ringslingers/Disable AssetBundles in Playmode (disables mods in editor)", validate = true)]
    private static bool UseEditorAssetsInPlaymode_Validate()
    {
        Menu.SetChecked("Ringslingers/Disable AssetBundles in Playmode (disables mods in editor)", shouldUseEditorAssetsInPlaymode);
        return true;
    }

    [MenuItem("Ringslingers/Disable AssetBundles for Playtest Builds (disables mods in build)", priority = 31)]
    private static void BuildModlessVersionForPlaytests()
    {
        shouldBuildModlessVersionForPlaytests = !shouldBuildModlessVersionForPlaytests;

        if (shouldBuildModlessVersionForPlaytests)
            EditorUtility.DisplayDialog("In Unity, everything has a sacrifice", "When this is enabled, playtest builds are faster because they don't require AssetBundles to be up-to-date. However, mods are disabled in these builds.", "I understand");
        else
            EditorUtility.DisplayDialog("In Unity, everything has a sacrifice", "When this is disabled, mods are enabled in builds. However, you must Build Core AssetBundles as necessary for scene and prefab changes to take place.", "I understand");
    }

    [MenuItem("Ringslingers/Disable AssetBundles for Playtest Builds (disables mods in build)", validate = true)]
    private static bool BuildModlessVersionForPlaytests_Validate()
    {
        Menu.SetChecked("Ringslingers/Disable AssetBundles for Playtest Builds (disables mods in build)", shouldBuildModlessVersionForPlaytests);
        return true;
    }

    /// <summary>
    /// Builds asset bundles for Ringslingers
    /// 
    /// Usually, you want includeCoreAssets to be true to ensure any added mods use those assets rather than duplicating them (which causes trouble)
    /// IncludeCoreScenes only needs to be true if you want to build the core scenes
    /// </summary>
    public static AssetBundleManifest BuildAssetBundles(bool includeCoreAssets, bool includeCoreScenes, string buildPath, string[] modContentDatabasePathsToExport, ModExportCopyFlags copyFlags, string additionalCopyPath = null)
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

        // User-friendly error checking
        bool hasError = false;
        foreach (AssetBundleBuild bundleBuild in bundleBuilds)
        {
            int numSceneAssets = 0;
            int numNonSceneAssets = 0;
            foreach (string assetName in bundleBuild.assetNames)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(assetName) == typeof(SceneAsset))
                    numSceneAssets++;
                else
                    numNonSceneAssets++;
            }

            if (numSceneAssets > 0 && numNonSceneAssets > 0)
            {
                if (!hasError)
                    EditorUtility.DisplayDialog("Unity plz", $"Asset bundle {bundleBuild.assetBundleName} has scene assets and non-scene assets. Unfortunately asset bundles are limited to either scenes or assets only and they must be separate.\n\nCheck log for problem assets.", "aight m8");

                StringBuilder sceneSb = new StringBuilder();
                StringBuilder assetSb = new StringBuilder();
                foreach (string assetName in bundleBuild.assetNames)
                {
                    StringBuilder activeSb = AssetDatabase.GetMainAssetTypeAtPath(assetName) == typeof(SceneAsset) ? sceneSb : assetSb;
                    activeSb.Append(assetName);
                    activeSb.Append(", ");
                }
                Debug.LogError($"Asset bundle {bundleBuild.assetBundleName} has both scene and non-scene assets.\n\nScene assets: {sceneSb.ToString()}\nNon-scene assets: {assetSb.ToString()}");

                hasError = true;
            }
        }
        if (hasError)
        {
            return null;
        }

        // Prepare to build
        BuildAssetBundlesParameters buildParams = new BuildAssetBundlesParameters()
        {
            bundleDefinitions = bundleBuilds.ToArray(),
            targetPlatform = EditorUserBuildSettings.activeBuildTarget,
            options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableLoadAssetByFileName | BuildAssetBundleOptions.DisableLoadAssetByFileNameWithExtension,
            outputPath = buildPath
        };

        if (!System.IO.Directory.Exists(buildPath))
            System.IO.Directory.CreateDirectory(buildPath);

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
            string buildFolder = System.IO.Path.GetFullPath(RingslingersCoreLoader.gameBuildDirectory);
            int numFilesCopied = 0;

            if ((copyFlags & (ModExportCopyFlags.CopyToBuildCoreFolder | ModExportCopyFlags.CopyToBuildModsFolder)) != 0)
            {
                foreach (string gameBuildDirectory in System.IO.Directory.EnumerateDirectories(buildFolder, "*", new System.IO.EnumerationOptions() { RecurseSubdirectories = true }))
                {
                    string directoryName = System.IO.Path.GetFileName(gameBuildDirectory);

                    if (directoryName == $"{Application.productName}_Data")
                    {
                        string buildCoreFolder = $"{gameBuildDirectory}";
                        string buildModFolder = $"{gameBuildDirectory}/Mods";

                        foreach (AssetBundleBuild bundleBuild in bundleBuilds)
                        {
                            numFilesCopied++;

                            if ((copyFlags & ModExportCopyFlags.CopyToBuildCoreFolder) != 0)
                                System.IO.File.Copy($"{buildPath}/{bundleBuild.assetBundleName}", $"{buildCoreFolder}/{bundleBuild.assetBundleName}", true);
                            if ((copyFlags & ModExportCopyFlags.CopyToBuildModsFolder) != 0)
                            {
                                if (!System.IO.Directory.Exists(buildModFolder))
                                    System.IO.Directory.CreateDirectory(buildModFolder);

                                System.IO.File.Copy($"{buildPath}/{bundleBuild.assetBundleName}", $"{buildModFolder}/{bundleBuild.assetBundleName}", true);
                            }
                        }
                    }
                }
            }

            // and to the additional copy target if provided
            if ((copyFlags & ModExportCopyFlags.CopyToAdditionalCopyPath) != 0)
            {
                if (!System.IO.Directory.Exists(additionalCopyPath))
                    System.IO.Directory.CreateDirectory(additionalCopyPath);

                foreach (AssetBundleBuild bundleBuild in bundleBuilds)
                    System.IO.File.Copy($"{buildPath}/{bundleBuild.assetBundleName}", $"{additionalCopyPath}/{bundleBuild.assetBundleName}", true);
            }

            if (numFilesCopied > 0)
                Debug.Log($"Copied asset bundle files to builds in Builds folder ({numFilesCopied} copies made)");
        }

        return manifest;
    }

    private static void OnPreBuild(ref BuildPlayerOptions buildPlayerOptions)
    {
        Debug.Log("Running prebuild");

        if (shouldBuildModlessVersionForPlaytests)
        {
            string[] extraScriptingDefines = buildPlayerOptions.extraScriptingDefines;
            if (extraScriptingDefines != null)
                System.Array.Resize(ref extraScriptingDefines, extraScriptingDefines.Length + 1);
            else
                extraScriptingDefines = new string[1];
            extraScriptingDefines[extraScriptingDefines.Length - 1] = "DISABLE_ASSETBUNDLES";

            buildPlayerOptions.extraScriptingDefines = extraScriptingDefines;
        }
    }
}
