using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    public Slider slider;

    private void Awake()
    {
        slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float val)
    {
        GamePreferences.globalVolume = val;
    }

    private void OnEnable()
    {
        slider.value = GamePreferences.globalVolume;
    }

    private void OnValidate()
    {
        if (slider == null)
            slider = GetComponent<Slider>();
    }
}
