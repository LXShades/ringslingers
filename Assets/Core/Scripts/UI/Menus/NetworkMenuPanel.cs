using UnityEngine;

public class NetworkMenuPanel : MonoBehaviour
{
    public SliderNumberPair extraSmoothing;
    public SliderNumberPair serverRewinding;
    public SliderNumberPair playerSmoothing;

    bool hasRegisteredCallbacks = false;

    private void OnEnable()
    {
        if (!hasRegisteredCallbacks)
        {
            extraSmoothing.onValueChanged.AddListener(value => GamePreferences.inputSmoothing = value * 0.001f);
            serverRewinding.onValueChanged.AddListener(value => GamePreferences.serverRewindTolerance = value * 0.001f);
            playerSmoothing.onValueChanged.AddListener(value => GamePreferences.opponentSmoothing = value);
            hasRegisteredCallbacks = true;
        }

        extraSmoothing.value = GamePreferences.inputSmoothing * 1000f;
        serverRewinding.value = GamePreferences.serverRewindTolerance * 1000f;
        playerSmoothing.value = GamePreferences.kDefaultOpponentSmoothing;
    }

    public void ResetToDefault()
    {
        extraSmoothing.value = GamePreferences.kDefaultInputSmoothing * 1000f;
        serverRewinding.value = GamePreferences.kDefaultServerRewinding * 1000f;
        playerSmoothing.value = GamePreferences.kDefaultOpponentSmoothing;
    }
}
