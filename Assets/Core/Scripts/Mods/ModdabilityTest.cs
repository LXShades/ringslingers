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
        RingslingersMod[] mods = new RingslingersMod[] { 
            new RingslingersMod { filename = $"{modNameToLoad}.Assets" },
            new RingslingersMod { filename = $"{modNameToLoad}.Scenes" },
        };
        ModManager.LoadMods(mods, (wasSuccessful, message) => OnFinishedBundleLoad(wasSuccessful, message));
    }

    private void OnFinishedBundleLoad(bool wasSuccessful, string message)
    {
        Debug.Log($"Mod loader: Success={wasSuccessful}, Message={message}");
    }
}
