using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResolutionSelector : MonoBehaviour
{
    public TMP_Dropdown list;

    private void OnEnable()
    {
        list.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        int currentResolution = -1;
        for (int i = Screen.resolutions.Length - 1; i >= 0; i--)
        {
            Resolution res = Screen.resolutions[i];
            options.Add(new TMP_Dropdown.OptionData($"{res.width}x{res.height} @ {res.refreshRateRatio.value}"));

            if (res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height && res.refreshRateRatio.value == Screen.currentResolution.refreshRateRatio.value)
                currentResolution = i;
        }

        list.options = options;

        if (currentResolution != -1)
            list.value = currentResolution;

        list.onValueChanged.AddListener(OnSelectionChanged);
    }

    private void OnDisable()
    {
        list.onValueChanged.RemoveListener(OnSelectionChanged);
    }

    private void OnSelectionChanged(int newIndex)
    {
        if (newIndex >= 0 && newIndex < Screen.resolutions.Length)
        {
            newIndex = Screen.resolutions.Length - 1 - newIndex; // resolutions are added in descending order
            Screen.SetResolution(Screen.resolutions[newIndex].width, Screen.resolutions[newIndex].height, Screen.fullScreenMode, Screen.resolutions[newIndex].refreshRateRatio);
        }
    }

    private void OnValidate()
    {
        if (list == null)
        {
            list = GetComponentInChildren<TMP_Dropdown>();
        }
    }
}
