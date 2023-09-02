using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModdabilityTest : MonoBehaviour
{
    public string modNameToLoad;

    private void Awake()
    {
        RingslingersMod[] mods = new RingslingersMod[] { new RingslingersMod { filename = modNameToLoad } };
        ModManager.LoadMods(mods, (wasSuccessful, message) => OnFinishedBundleLoad(wasSuccessful, mods[0].loadedAssetBundle));
    }

    private void OnFinishedBundleLoad(bool wasSuccessful, AssetBundle loadedMod)
    {
        Debug.Log($"Mod loader success? {wasSuccessful}");

        if (NetworkServer.active)
        {
            // todo: add into the map rotation
            //Netplay.singleton.ServerLoadLevel(loadedMod.GetAllScenePaths()[0]);
        }
    }
}
