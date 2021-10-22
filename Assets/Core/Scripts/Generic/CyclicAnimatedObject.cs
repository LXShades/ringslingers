using UnityEngine;

/// <summary>
/// Follows an animation cyclically, tries to sync to serverTime
/// </summary>
public class CyclicAnimatedObject : MonoBehaviour
{
    public AnimationClip animationToCycle;

    void Update()
    {
        animationToCycle.SampleAnimation(gameObject, GameTicker.singleton.predictedServerTime % animationToCycle.length);
    }
}
