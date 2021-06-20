using UnityEngine;
using System.Collections.Generic;

public class SpeedTrails : MonoBehaviour
{
    [Header("Hierarchy")]
    public TrailRenderer trails;

    [Header("Passive trails")]
    public bool enablePassiveSpeedTrail = false;
    public AnimationCurve opacityBySpeed = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Thok pulse")]
    public float thokPulseDuration = 0.1f;

    [Header("Rolling")]
    public float opacityWhileRolling = 0.4f;

    private bool hasPulsedSinceLand = false;

    private Timer thokPulseProgress = new Timer();

    private CharacterMovement movement;

    private readonly List<GradientAlphaKey> trailAlphas = new List<GradientAlphaKey>(256);

    private readonly GradientAlphaKey[] trailAlphasAsArray = new GradientAlphaKey[8];
    private readonly GradientColorKey[] trailMainColour = new GradientColorKey[1];

    private void Start()
    {
        movement = GetComponent<CharacterMovement>();
    }

    private void Update()
    {
        float opacity = 0f;

        // thok pulses
        if ((movement.state & CharacterMovement.State.Thokked) != 0)
        {
            if (!hasPulsedSinceLand)
            {
                hasPulsedSinceLand = true;
                thokPulseProgress.Start(thokPulseDuration);
            }
        }
        else
        {
            hasPulsedSinceLand = false;
        }

        // decide opacity
        if (enablePassiveSpeedTrail && movement)
            opacity = Mathf.Max(opacity, opacityBySpeed.Evaluate(movement.velocity.magnitude));

        if (thokPulseProgress.isRunning)
            opacity = Mathf.Max(opacity, 1f - thokPulseProgress.progress);

        if ((movement.state & CharacterMovement.State.Rolling) != 0)
            opacity = Mathf.Max(opacity, opacityWhileRolling);

        // handle alpha along the trail
        trailAlphas.Insert(0, new GradientAlphaKey(opacity, 0f));
        float timeShift = Time.deltaTime / trails.time;
        for (int i = 1; i < trailAlphas.Count; i++)
            trailAlphas[i] = new GradientAlphaKey(trailAlphas[i].alpha, trailAlphas[i].time + timeShift);

        for (int i = trailAlphas.Count - 1; i >= 0; i--)
        {
            if (trailAlphas[i].time > 1f)
                trailAlphas.RemoveAt(i);
        }

        float timePerSample = 1f / trailAlphasAsArray.Length;
        int sourceIndex = 0;
        for (int i = 0; i < trailAlphasAsArray.Length; i++)
        {
            float t = timePerSample * i;
            while (sourceIndex < trailAlphas.Count && trailAlphas[sourceIndex].time < t)
                sourceIndex++;

            trailAlphasAsArray[i] = new GradientAlphaKey(sourceIndex < trailAlphas.Count ? trailAlphas[sourceIndex].alpha : 0f, t);
        }

        // update trail
        trailMainColour[0] = new GradientColorKey(new Color(trails.startColor.r, trails.startColor.g, trails.startColor.b), 0f);
        trails.colorGradient.SetKeys(trailMainColour, trailAlphasAsArray);
        trails.emitting = opacity >= 0.05f;
    }
}
