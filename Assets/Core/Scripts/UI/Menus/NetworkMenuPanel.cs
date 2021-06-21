using UnityEngine;

public class NetworkMenuPanel : MonoBehaviour
{
    public SliderNumberPair minClientDelay;
    public SliderNumberPair maxClientDelay;
    public SliderNumberPair minServerDelay;
    public SliderNumberPair maxServerDelay;

    bool hasRegisteredCallbacks = false;

    private void OnEnable()
    {
        if (!hasRegisteredCallbacks)
        {
            minClientDelay.onValueChanged.AddListener((float value) => GamePreferences.minClientDelayMs = value);
            maxClientDelay.onValueChanged.AddListener((float value) => GamePreferences.maxClientDelayMs = value);
            minServerDelay.onValueChanged.AddListener((float value) => GamePreferences.minServerDelayMs = value);
            maxServerDelay.onValueChanged.AddListener((float value) => GamePreferences.maxServerDelayMs = value);
            hasRegisteredCallbacks = true;
        }

        minClientDelay.value = Mathf.Min(GamePreferences.minClientDelayMs, GamePreferences.maxClientDelayMs);
        maxClientDelay.value = GamePreferences.maxClientDelayMs;
        minServerDelay.value = Mathf.Min(GamePreferences.minServerDelayMs, GamePreferences.maxServerDelayMs);
        maxServerDelay.value = GamePreferences.maxServerDelayMs;
    }

    public void ResetToDefault()
    {
        minClientDelay.value = Netplay.kDefaultMinClientDelay;
        maxClientDelay.value = Netplay.kDefaultMaxClientDelay;
        minServerDelay.value = Netplay.kDefaultMinServerDelay;
        maxServerDelay.value = Netplay.kDefaultMaxServerDelay;
    }
}
