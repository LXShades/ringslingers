using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SliderNumberPair : MonoBehaviour
{
    public Slider slider;
    public InputField number;

    public float value
    {
        set
        {
            slider.minValue = minValue;
            slider.maxValue = maxValue; // in case awake not yet called
            slider.value = value;
            number.text = value.ToString();
        }
        get => slider.value;
    }

    public float minValue = 0f;
    public float maxValue = 1f;

    public UnityEvent<float> onValueChanged;

    private bool suppressCallbacks = false;

    private void Awake()
    {
        if (slider && number)
        {
            number.onValueChanged.AddListener(OnNumberChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);

            slider.minValue = minValue;
            slider.maxValue = maxValue;
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

    private void OnValidate()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>();
        if (number == null)
            number = GetComponentInChildren<InputField>();
    }
}
