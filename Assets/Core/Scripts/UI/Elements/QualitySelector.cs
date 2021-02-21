using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QualitySelector : MonoBehaviour
{
    public Dropdown dropdown;

    private void Awake()
    {
        dropdown.ClearOptions();

        List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
        foreach (string qualityLevelName in QualitySettings.names)
        {
            options.Add(new Dropdown.OptionData()
            {
                text = qualityLevelName
            });
        }

        dropdown.AddOptions(options);
        dropdown.value = QualitySettings.GetQualityLevel();

        dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnValueChanged(int value)
    {
        if (value != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(value);
    }
}
