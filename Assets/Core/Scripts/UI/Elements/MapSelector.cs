using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelector : MonoBehaviour
{
    public Dropdown dropdown;

    private void OnEnable()
    {
        List<LevelConfiguration> levels = RingslingersContent.loaded.levels;

        if (levels.Count == 0)
            return;
        dropdown.ClearOptions();

        int selectionIndex = -1;
        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
        for (int i = 0; i < levels.Count; i++)
        {
            LevelConfiguration level = levels[i];
            options.Add(new Dropdown.OptionData($"{level.friendlyName} - {level.credits}"));

            if (level.path.Equals(SceneManager.GetActiveScene().path, System.StringComparison.CurrentCultureIgnoreCase))
                selectionIndex = i;
        }

        dropdown.AddOptions(options);
        dropdown.value = selectionIndex;
    }

    public void GoToSelectedMap()
    {
        Debug.Assert(NetMan.singleton.mode != Mirror.NetworkManagerMode.ClientOnly);

        Netplay.singleton.ServerLoadLevel(RingslingersContent.loaded.levels[dropdown.value]);
    }

    private void OnValidate()
    {
        if (dropdown == null)
            dropdown = GetComponent<Dropdown>();
    }
}
