using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QualitySelector : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (string qualityLevelName in QualitySettings.names)
        {
            options.Add(new TMP_Dropdown.OptionData()
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
