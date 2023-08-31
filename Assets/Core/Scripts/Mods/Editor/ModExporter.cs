using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ModExporter
{
    public static string editorModFolder => System.IO.Path.Combine(Application.dataPath, "Mods");

    [MenuItem("Tools/Ringslingers/Export Mod...")]
    public static void ExportMod()
    {
        string exportPath = EditorUtility.SaveFilePanel("Export Mod", System.IO.Directory.GetCurrentDirectory(), "MyAwesomeMod.assetbundle", "assetbundle");

        if (string.IsNullOrEmpty(exportPath))
            return;

        // todo: character prefabs as well
        string[] scenesToInclude = System.IO.Directory.EnumerateFiles(editorModFolder, "*.unity").ToArray();

        string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../"));

        for (int i = 0; i < scenesToInclude.Length; i++)
            scenesToInclude[i] = System.IO.Path.GetRelativePath(projectRoot, scenesToInclude[i]);

        if (scenesToInclude.Length == 0)
        {
            EditorUtility.DisplayDialog("No mods found", $"No scenes were found in the mod folder:\n\n{editorModFolder}", "OK");
            return;
        }

        AssetBundleBuild primaryBundle = new AssetBundleBuild()
        {
            assetNames = scenesToInclude,
            assetBundleName = System.IO.Path.GetFileName(exportPath)
        };
        BuildAssetBundlesParameters buildParams = new BuildAssetBundlesParameters()
        {
             bundleDefinitions = new AssetBundleBuild[] { primaryBundle },
             targetPlatform = EditorUserBuildSettings.activeBuildTarget,
             options = BuildAssetBundleOptions.None,
             subtarget = 0, // what is this?
             outputPath = System.IO.Path.GetDirectoryName(exportPath)
        };

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(buildParams);

#if UNITY_EDITOR_WIN
        Process.Start("explorer.exe", System.IO.Path.GetDirectoryName(exportPath));
#endif
    }
}
