using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FullscreenSelector : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    public void OnEnable()
    {
        dropdown.ClearOptions();

        dropdown.AddOptions(new List<TMP_Dropdown.OptionData>(
            new TMP_Dropdown.OptionData[] { new TMP_Dropdown.OptionData("Windowed"), new TMP_Dropdown.OptionData("Fullscreen"), new TMP_Dropdown.OptionData("Fullscreen Borderless") }));

        switch (Screen.fullScreenMode)
        {
            case FullScreenMode.Windowed: dropdown.value = 0; break;
            case FullScreenMode.ExclusiveFullScreen: dropdown.value = 1; break;
            case FullScreenMode.FullScreenWindow: dropdown.value = 2; break;
        }

        dropdown.onValueChanged.AddListener(SetFullscreenMode);
    }

    public void SetFullscreenMode(int index)
    {
        switch (index)
        {
            case 0: Screen.fullScreenMode = FullScreenMode.Windowed; break;
            case 1: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
            case 2: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
        }
    }
}
