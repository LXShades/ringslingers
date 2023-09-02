using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCharacterSelector : MonoBehaviour
{
    public Dropdown dropdown;

    void Awake()
    {
        dropdown = GetComponent<Dropdown>();
        dropdown.onValueChanged.AddListener(OnCharacterChanged);
    }

    private void Start()
    {
        if (dropdown)
            PopulateDropdown();
    }

    void PopulateDropdown()
    {
        dropdown.ClearOptions();

        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();

        foreach (CharacterConfiguration characterChoice in RingslingersContent.loaded.characters)
            options.Add(new Dropdown.OptionData() { text = characterChoice.name });

        dropdown.AddOptions(options);
    }

    void OnCharacterChanged(int index)
    {
        LocalPersistentPlayer persistent = Player.localPersistent;

        persistent.characterIndex = index;
        Player.localPersistent = persistent;
    }
}
