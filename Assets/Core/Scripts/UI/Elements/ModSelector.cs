using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ModSelector : MonoBehaviour
{
    public Transform contentArea;

    public ModButton modButtonPrefab;

    private List<ModButton> spawnedModButtons = new List<ModButton>();

    public void OnEnable()
    {
        PopulateMods();
    }

    private void PopulateMods()
    {
        foreach (ModButton mod in spawnedModButtons)
            Destroy(mod.gameObject);

        spawnedModButtons.Clear();

        // Add all assets mods and scene mods to the list
        HashSet<string> allFilesWithoutExtensions = new HashSet<string>(Directory.EnumerateFiles(ModManager.activeModDirectory).Select(x =>
        {
            string filename = System.IO.Path.GetFileName(x);
            int filenameFirstExtensionStart = filename.IndexOf(".");
            return x.Substring(0, x.Length - (filename.Length - filenameFirstExtensionStart));
        }));

        foreach (string file in allFilesWithoutExtensions)
        {
            string assetsPath = System.IO.Path.ChangeExtension(file, ".assets");
            string scenesPath = System.IO.Path.ChangeExtension(file, ".scenes");
            if (File.Exists(assetsPath)) // all mods need an assets file currently
            {
                ModButton spawnedModButton = Instantiate(modButtonPrefab, contentArea);

                spawnedModButton.modAssetsPath = assetsPath;
                if (File.Exists(System.IO.Path.ChangeExtension(file, ".scenes")))
                    spawnedModButton.modScenesPath = scenesPath;

                spawnedModButtons.Add(spawnedModButton);
            }
        }
    }
}
