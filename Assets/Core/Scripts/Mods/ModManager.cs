using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static ModManager;

[System.Serializable]
public class RingslingersMod
{
    public string filename;
    public ulong hash;
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
    public static string activeModDirectory => Application.isEditor ? modDirectoryForEditor : modDirectoryForBuilds;

    public static string modDirectoryForBuilds => System.IO.Path.Combine(Application.dataPath, "Mods");
    public static string modDirectoryForEditor => RingslingersCoreLoader.modBuildDirectory;

    public static Action<RingslingersMod> onAnyModLoaded;

    public static List<RingslingersMod> loadedMods = new List<RingslingersMod>();

    public static List<ModLoadProcess> modLoadProcesses = new List<ModLoadProcess>();

    private static AssetBundleCreateRequest modLoadOperation = null;

    /// <summary>
    /// Loads a set of mods. Note that some properties may be changed (filename)
    /// 
    /// TODO: input and output is the RingslingersMod provided, but they should probably be split into descriptor (filename, crc etc) and instance (loadedAssetBundle, etc)
    /// </summary>
    public static void LoadMods(RingslingersMod[] mods, LoadedModsDelegate onFinished)
    {
        if (!RingslingersCoreLoader.areAssetBundlesEnabled)
        {
            Debug.LogError("Tried to load mods but AssetBundles are disabled in this run of the game.");
            onFinished?.Invoke(false, "Mods are disabled");
            return;
        }

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

            if (!System.IO.File.Exists(System.IO.Path.Combine(activeModDirectory, mod.filename)))
                errors.AppendLine($"Mod \"{mod.filename}\" could not be found in {activeModDirectory}");

            // strip path from the filename if there is one (there shouldn't really be a path)
            mod.filename = System.IO.Path.GetFileName(mod.filename);
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

                modLoadOperation = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(activeModDirectory, nextModToLoad.filename));
                modLoadOperation.completed += op => OnSingleModOperationFinished();
            }
            else
            {
                modLoadProcesses.RemoveAt(0); // this shouldn't really happen, it should be cleaned up in OnSingleModLoaded. But remove it just in case
            }
        }
    }

    public static ulong GetModHash(string modName)
    {
        try
        {
            using (var sha256 = SHA256.Create())
            {
                using (System.IO.FileStream stream = System.IO.File.OpenRead(System.IO.Path.Combine(activeModDirectory, modName)))
                {
                    byte[] hash = sha256.ComputeHash(stream);

                    return (ulong)BitConverter.ToInt64(hash, 0) ^ (ulong)BitConverter.ToInt64(hash, 8) ^ (ulong)BitConverter.ToInt64(hash, 16) ^ (ulong)BitConverter.ToInt64(hash, 24);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        return 0;
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
            modLoadProcess.modBeingLoaded.hash = GetModHash(modLoadProcess.modBeingLoaded.filename);

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
                            RingslingersContent.LoadContent(contentDatabase.content);
                    }
                }

                // Add to the loaded mods list
                loadedMods.AddRange(modLoadProcess.modsLoaded);

                // Then notify that it's loaded and move to next if applicable
                try
                {
                    modLoadProcess.onLoadProcessFinished?.Invoke(true, "Mod load successful");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    modLoadProcesses.RemoveAt(0);
                }

                try
                {
                    foreach (RingslingersMod loadedMod in modLoadProcess.modsLoaded)
                        onAnyModLoaded?.Invoke(loadedMod);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
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
