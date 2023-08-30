using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModdabilityTest : MonoBehaviour
{
    public static string modPath => System.IO.Path.Combine(Application.dataPath, "Mods");

    public string modNameToLoad;

    private void Awake()
    {
        AssetBundleCreateRequest loader = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(modPath, modNameToLoad));
        loader.completed += OnFinishedBundleLoad;
    }

    private void OnFinishedBundleLoad(AsyncOperation op)
    {
        AssetBundle loadedMod = (op as AssetBundleCreateRequest).assetBundle;

        // todo: add into the map rotation
        NetMan.singleton.ServerChangeScene(loadedMod.GetAllScenePaths()[0], true);
    }
}
