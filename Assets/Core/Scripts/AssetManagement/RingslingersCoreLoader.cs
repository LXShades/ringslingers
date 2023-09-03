using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ringslingers Core Loader is responsible for loading Ringslingers Common assets
/// 
/// I don't know whether to call them Common or Core assets lol
/// 
/// Anyway they are needed for modding support.
/// It'd be great to just have the scenes in the build settings and not worry about AssetBundles
/// Bbut for mods to be able to reuse the same assets as the main game, those assets need to be in a separate AssetBundle that we're calling RingslingersCommon.
/// Otherwise, any mods using those assets would create duplicates of those assets. That'd be size-inefficient and mess with networking asset lookups as well. C'est la vie.
/// </summary>
public class RingslingersCoreLoader : MonoBehaviour
{
    // Of course, you gotta keep this up to date!
    public const string bootAssetPath = "Assets/Core/RingslingersCore.Assets/Prefabs/Boot.prefab";

    public static string gameBuildPath = $"{Application.dataPath}/../Builds";
    public static string coreAssetsBuildPath = $"{Application.dataPath}/../Builds/AssetBundles";

    public const string coreAssetBundleName = "ringslingerscore.assets";
    public const string coreSceneBundleName = "ringslingerscore.scenes";

    public static bool useEditorAssetsIfAvailable = true;
    private static bool isUsingEditorAssets = false;

    private static AssetBundle commonAssets = null;
    private static AssetBundle commonScenes = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void OnGameStarted()
    {
        // Start loading the core game content as early as possible
        LoadCoreContent();
    }

    private void Awake()
    {
        if (commonAssets != null || isUsingEditorAssets)
        {
#if UNITY_EDITOR
            Debug.LogWarning("Using EDITOR ASSETS to run the game. These are different to the assets used in builds and do not support mods.");
            GameObject bootAsset = isUsingEditorAssets ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(bootAssetPath) : commonAssets.LoadAsset<GameObject>(bootAssetPath);
#else
            GameObject bootAsset = commonAssets.LoadAsset<GameObject>(bootAssetPath);
#endif

            if (bootAsset != null)
            {
                Instantiate(bootAsset);
            }
            else
            {
                Debug.LogError("Fatal erorr occurred while trying to load Ringslingers Core content. The Boot asset could not be found.");
                Application.Quit();
            }
        }
        else
        {
            Debug.LogError("Fatal error occurred while trying to load Ringslingers Core content");
            Application.Quit();
        }
    }

    private static void LoadCoreContent()
    {
        Debug.Log("Loading Ringslingers Core content...");

#if UNITY_EDITOR
        if (useEditorAssetsIfAvailable)
        {
            isUsingEditorAssets = true;
        }
        else
        {
            commonAssets = AssetBundle.LoadFromFile($"{coreAssetsBuildPath}/{coreAssetBundleName}");
            commonScenes = AssetBundle.LoadFromFile($"{coreAssetsBuildPath}/{coreSceneBundleName}");
        }
#else
        commonAssets = AssetBundle.LoadFromFile(coreAssetBundleName);
        commonScenes = AssetBundle.LoadFromFile(coreSceneBundleName);
#endif


    }
}
