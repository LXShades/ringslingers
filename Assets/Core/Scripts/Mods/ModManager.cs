using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RingslingersMod
{
    public string filename;
    public uint crc;
    public string url; // todo

    // don't serialize the following
    public AssetBundle loadedAssetBundle { get; set; }
}

public class ModManager
{
    public delegate void LoadedModsDelegate(bool wasSuccessful, string message);
    public static string modPath => System.IO.Path.Combine(Application.dataPath, "Mods");

    public static List<RingslingersMod> loadedMods = new List<RingslingersMod>();

    public static List<RingslingersMod> loadingMods = new List<RingslingersMod>();

    private static AssetBundleCreateRequest modLoadOperation = null;

    private static LoadedModsDelegate onAllModsLoadedDelegate;

    /// <summary>
    /// Loads a set of mods. Note that some properties may be changed (filename)
    /// </summary>
    public static void LoadMods(RingslingersMod[] mods, LoadedModsDelegate onFinished)
    {
        StringBuilder errors = new StringBuilder();

        // Check if we can first
        foreach (RingslingersMod mod in mods)
        {
            if (string.IsNullOrEmpty(mod.filename))
            {
                errors.AppendLine($"Null or empty mod supplied");
                continue;
            }

            // Add extension if missing
            if (!System.IO.Path.HasExtension(mod.filename))
                mod.filename = System.IO.Path.ChangeExtension(mod.filename, "assetbundle");

            if (!System.IO.File.Exists(System.IO.Path.Combine(modPath, mod.filename)))
                errors.AppendLine($"Mod \"{mod.filename}\" could not be found in {modPath}");
        }

        if (errors.Length > 0)
        {
            onFinished?.Invoke(false, errors.ToString());
            return;
        }

        // Try loading them all
        loadingMods.AddRange(mods);

        onAllModsLoadedDelegate += onFinished;
        LoadNextModIfNotAlready();
    }

    private static void LoadNextModIfNotAlready()
    {
        if (loadingMods.Count == 0)
            return;

        if (modLoadOperation == null)
        {
            modLoadOperation = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(modPath, loadingMods[0].filename));
            modLoadOperation.completed += op =>
            {
                if (modLoadOperation.assetBundle == null)
                {
                    Debug.LogError($"ERROR loading mod {loadingMods[0].filename}");
                }

                loadingMods[0].loadedAssetBundle = modLoadOperation.assetBundle;
                loadedMods.Add(loadingMods[0]);
                loadingMods.RemoveAt(0);

                // Continue or end the load process
                if (loadingMods.Count == 0)
                    OnModLoadCompleted();
                else
                    LoadNextModIfNotAlready();
            };
        }
    }

    private static void OnModLoadCompleted()
    {
        onAllModsLoadedDelegate?.Invoke(true, "ersertjerlkj - TODO");
        onAllModsLoadedDelegate -= onAllModsLoadedDelegate;
    }
}
