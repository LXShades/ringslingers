using UnityEngine;

public class ControlConfigMenu : MonoBehaviour
{
    public SliderNumberPair mouseSpeedSlider;

    private void Start()
    {
        mouseSpeedSlider.value = 1f / Mathf.Max(GamePreferences.mouseSpeed, 0.001f);

        mouseSpeedSlider.onValueChanged.AddListener(OnMouseSpeedChanged);
    }

    private void OnMouseSpeedChanged(float value)
    {
        GamePreferences.mouseSpeed = 1f / Mathf.Max(value, 1f);
    }

    public void Save()
    {
        GamePreferences.Save();
    }
}
