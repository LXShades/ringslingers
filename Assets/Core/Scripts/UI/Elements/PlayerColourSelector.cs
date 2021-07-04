using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerColourSelector : MonoBehaviour
{
    [System.Serializable]
    public struct ColorNamePair
    {
        public string name;
        public Color colour;
    }

    public ColorNamePair[] colourChoices = new ColorNamePair[0];

    private Dropdown dropdown;

    void Awake()
    {
        dropdown = GetComponent<Dropdown>();
        dropdown.onValueChanged.AddListener(OnColourChanged);

        if (dropdown)
            PopulateDropdown();
    }

    void PopulateDropdown()
    {
        dropdown.ClearOptions();

        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();

        foreach (ColorNamePair colourChoice in colourChoices)
            options.Add(new Dropdown.OptionData() { text = colourChoice.name });

        dropdown.AddOptions(options);
    }

    void OnColourChanged(int index)
    {
        Color colour = colourChoices[index].colour;
        colour.a = 1;

        if (Netplay.singleton && index >= 0 && index < colourChoices.Length)
        {
            LocalPersistentPlayer persistent = Player.localPersistent;
            persistent.colour = colour;
            Player.localPersistent = persistent;
        }

        dropdown.colors = new ColorBlock()
        {
            normalColor = colour,
            colorMultiplier = 1,
            highlightedColor = colour,
            pressedColor = colour,
            selectedColor = colour
        };
    }
}
