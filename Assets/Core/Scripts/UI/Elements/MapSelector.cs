using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MapSelector : MonoBehaviour
{
    public Dropdown dropdown;

    private void OnEnable()
    {
        LevelDatabase db = GameManager.singleton.levelDatabase;

        if (db.levels.Length == 0)
            return;
        dropdown.ClearOptions();

        int selectionIndex = -1;
        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
        for (int i = 0; i < db.levels.Length; i++)
        {
            LevelDatabase.Level level = db.levels[i];
            options.Add(new Dropdown.OptionData(level.configuration.friendlyName));

            if (level.path.Equals(SceneManager.GetActiveScene().path, System.StringComparison.CurrentCultureIgnoreCase))
                selectionIndex = i;
        }

        dropdown.AddOptions(options);
        dropdown.value = selectionIndex;
    }

    public void GoToSelectedMap()
    {
        Debug.Assert(NetMan.singleton.mode != Mirror.NetworkManagerMode.ClientOnly);

        LevelDatabase db = GameManager.singleton.levelDatabase;

        NetMan.singleton.ServerChangeScene(db.levels[dropdown.value].path, true);
    }

    private void OnValidate()
    {
        if (dropdown == null)
            dropdown = GetComponent<Dropdown>();
    }
}
