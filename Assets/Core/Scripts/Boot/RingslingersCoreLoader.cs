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
    public static string coreAssetsBuildPath = $"{Application.dataPath}/../Builds/AssetBundles";

    public const string coreAssetBundleName = "ringslingerscore.assets";
    public const string coreSceneBundleName = "ringslingerscore.scenes";

    private void Awake()
    {
        Debug.Log("Loading Ringslingers Core content...");

#if UNITY_EDITOR
        AssetBundle commonAssets = AssetBundle.LoadFromFile($"{coreAssetsBuildPath}/{coreAssetBundleName}");
        AssetBundle commonScenes = AssetBundle.LoadFromFile($"{coreAssetsBuildPath}/{coreSceneBundleName}");
#else
        AssetBundle commonAssets = AssetBundle.LoadFromFile(coreAssetBundleName);
        AssetBundle commonScenes = AssetBundle.LoadFromFile(coreSceneBundleName);
#endif

        if (commonAssets != null)
        {
            GameObject bootAsset = commonAssets.LoadAsset<GameObject>("Boot");
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
}
