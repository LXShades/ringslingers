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
    public string buildDate;

    // don't serialize the following
    public AssetBundle loadedAssetBundle { get; set; }
}

public class ModManager
{
    public class ModLoadProcess
    {
        public List<RingslingersMod> modsToLoad = new List<RingslingersMod>();
        public RingslingersMod modBeingLoaded = null;
        public List<RingslingersMod> modsLoaded = new List<RingslingersMod>();
        public LoadedModsDelegate onLoadProcessFinished;

        public ModLoadProcess(IEnumerable<RingslingersMod> inModsToLoad)
        {
            modsToLoad.AddRange(inModsToLoad);
        }
    }

    public delegate void LoadedModsDelegate(bool wasSuccessful, string message);
    public static string modPath => System.IO.Path.Combine(Application.dataPath, "Mods");

    public static List<RingslingersMod> loadedMods = new List<RingslingersMod>();

    public static List<ModLoadProcess> modLoadProcesses = new List<ModLoadProcess>();

    private static AssetBundleCreateRequest modLoadOperation = null;

    /// <summary>
    /// Loads a set of mods. Note that some properties may be changed (filename)
    /// </summary>
    public static void LoadMods(RingslingersMod[] mods, LoadedModsDelegate onFinished)
    {
#if UNITY_EDITOR
        if (RingslingersCoreLoader.useEditorAssetsIfAvailable)
        {
            Debug.LogError("Tried to load mods but Use Editor Assets in Playmode is enabled. We cannot currently load mods in that mode, unfortunately.");
        }
#endif

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
        modLoadProcesses.Add(new ModLoadProcess(mods)
        {
            onLoadProcessFinished = onFinished
        });
        LoadNextModIfNotAlready();
    }

    private static void LoadNextModIfNotAlready()
    {
        if (modLoadProcesses.Count == 0)
            return; // nothing left to load

        if (modLoadOperation == null)
        {
            ModLoadProcess modLoadProcess = modLoadProcesses[0];
            RingslingersMod nextModToLoad = null;

            if (modLoadProcess.modsToLoad.Count > 0)
            {
                nextModToLoad = modLoadProcess.modBeingLoaded = modLoadProcess.modsToLoad[0];
                modLoadProcess.modsToLoad.RemoveAt(0);

                modLoadOperation = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(modPath, nextModToLoad.filename));
                modLoadOperation.completed += op => OnSingleModOperationFinished();
            }
            else
            {
                modLoadProcesses.RemoveAt(0); // this shouldn't really happen, it should be cleaned up in OnSingleModLoaded. But remove it just in case
            }
        }
    }

    private static void OnSingleModOperationFinished()
    {
        if (modLoadProcesses.Count == 0)
        {
            Debug.LogError("Mod load operation finished but there was allegedly nothing loading - this shouldn't happen - programmer error.");
            return;
        }

        ModLoadProcess modLoadProcess = modLoadProcesses[0];

        if (modLoadOperation.assetBundle != null)
        {
            modLoadProcess.modBeingLoaded.loadedAssetBundle = modLoadOperation.assetBundle;
            modLoadProcess.modsLoaded.Add(modLoadProcess.modBeingLoaded);
            modLoadProcess.modsToLoad.Remove(modLoadProcess.modBeingLoaded);
            
            if (modLoadProcess.modsToLoad.Count == 0)
            {
                // The mod load is finished!
                // If this isn't the scenes bundle it should have the content database, so load that here
                foreach (RingslingersMod mod in modLoadProcess.modsLoaded)
                {
                    if (mod.loadedAssetBundle.GetAllAssetNames().Length > 0)
                    {
                        UnityEngine.Object mainAsset = mod.loadedAssetBundle.LoadAsset(mod.loadedAssetBundle.GetAllAssetNames()[0]);

                        if (mainAsset is RingslingersContentDatabase contentDatabase)
                        {
                            RingslingersContent.LoadContent(contentDatabase.content);
                        }
                    }
                }

                // Then notify that it's loaded and move to next if applicable
                try
                {
                    modLoadProcess.onLoadProcessFinished?.Invoke(true, "Mod load successful");
                }
                finally
                {
                    modLoadProcesses.RemoveAt(0);
                }
            }
        }
        else
        {
            // Error loading a mod, welp time to undo everything we did.
            foreach (RingslingersMod loadedMod in modLoadProcess.modsLoaded)
                loadedMod.loadedAssetBundle.Unload(true);

            try // we use try finally blocks just in case something goes wrong in the error handler, we don't want to softlock the mod process if we can avoid it.
            {
                modLoadProcesses[0].onLoadProcessFinished?.Invoke(false, $"Unknown error loading mod '{modLoadProcess.modBeingLoaded.filename}'");
            }
            finally
            {
                modLoadProcesses.RemoveAt(0);
            }
        }

        modLoadOperation = null;
        LoadNextModIfNotAlready();
    }
}
