using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SliderNumberPair : MonoBehaviour
{
    public Slider slider;
    public InputField number;

    public float value
    {
        set => slider.value = value;
        get => slider.value;
    }

    public UnityEvent<float> onValueChanged;

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
        if (!suppressCallbacks)
        {
            suppressCallbacks = true;
            number.text = sliderValue.ToString();
            suppressCallbacks = false;
            onValueChanged?.Invoke(sliderValue);
        }
    }

    private void OnNumberChanged(string number)
    {
        if (!suppressCallbacks)
        {
            suppressCallbacks = true;
            slider.value = Mathf.Clamp(float.Parse(number), slider.minValue, slider.maxValue);
            suppressCallbacks = false;
            onValueChanged?.Invoke(slider.value);
        }
    }
}
