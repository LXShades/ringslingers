using UnityEngine;
using UnityEngine.UI;

public class SliderNumberPair : MonoBehaviour
{
    public Slider slider;
    public InputField number;

    private bool suppressCallbacks = false;

    private void Awake()
    {
        if (slider && number)
        {
            number.onValueChanged.AddListener(OnNumberChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void OnSliderChanged(float sliderValue)
    {
        suppressCallbacks = true;
        number.text = sliderValue.ToString();
        suppressCallbacks = false;
    }

    private void OnNumberChanged(string number)
    {
        suppressCallbacks = true;
        slider.value = Mathf.Clamp(float.Parse(number), slider.minValue, slider.maxValue);
        suppressCallbacks = false;
    }
}
