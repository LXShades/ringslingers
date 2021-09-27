using UnityEngine;

public class NetworkMenuPanel : MonoBehaviour
{
    public SliderNumberPair extraSmoothing;

    bool hasRegisteredCallbacks = false;

    private void OnEnable()
    {
        if (!hasRegisteredCallbacks)
        {
            extraSmoothing.onValueChanged.AddListener((float value) => GamePreferences.extraSmoothing = value * 0.001f);
            hasRegisteredCallbacks = true;
        }

        extraSmoothing.value = GamePreferences.extraSmoothing * 1000f;
    }

    public void ResetToDefault()
    {
        extraSmoothing.value = GamePreferences.kDefaultExtraSmoothing * 1000f;
    }
}
