using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModButton : MonoBehaviour
{
    public TextMeshProUGUI text;
    public Button button;

    public string modName;
    public string modAssetsPath { get; set; }
    public string modScenesPath { get; set; }

    private void Start()
    {
        RefreshTextAndInteractivity();

        button.onClick.AddListener(OnClicked);
    }

    private void RefreshTextAndInteractivity()
    {
        if (ModManager.loadedMods.Find(x => string.Compare(System.IO.Path.GetFileName(x.filename), System.IO.Path.GetFileName(modAssetsPath), true) == 0) != null)
        {
            button.interactable = false; // Mod is already loaded
            text.text = $"<ADDED> {System.IO.Path.GetFileNameWithoutExtension(modAssetsPath)}";
        }
        else
        {
            text.text = System.IO.Path.GetFileNameWithoutExtension(modAssetsPath);
        }
    }

    private void OnClicked()
    {
        RingslingersMod[] modsToLoad;

        // We always have an assets path, if there's a scenes path we add that too
        if (modScenesPath != null)
            modsToLoad = new RingslingersMod[] { new RingslingersMod() { filename = System.IO.Path.GetFileName(modAssetsPath) }, new RingslingersMod() { filename = System.IO.Path.GetFileName(modScenesPath) } };
        else
            modsToLoad = new RingslingersMod[] { new RingslingersMod() { filename = System.IO.Path.GetFileName(modAssetsPath) } };

        ModManager.LoadMods(modsToLoad, false, (wasSuccessful, loadedMod) => {
            if (wasSuccessful)
                Debug.Log("Mod successfully loaded");
            else
                Debug.LogError("Mod load failed");

            RefreshTextAndInteractivity();
        });
    }
}
