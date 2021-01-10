using UnityEngine;

public class SpeedTrails : MonoBehaviour
{
    [Header("Hierarchy")]
    public TrailRenderer trails;

    [Header("Passive trails")]
    public bool enablePassiveSpeedTrail = false;
    public AnimationCurve opacityBySpeed = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Thok pulse")]
    public float thokPulseDuration = 0.1f;

    private bool hasPulsedSinceLand = false;

    private Timer thokPulseProgress = new Timer();

    private CharacterMovement movement;

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

        // passive opacity
        if (enablePassiveSpeedTrail && movement)
        {
            opacity = Mathf.Max(opacity, opacityBySpeed.Evaluate(movement.velocity.magnitude));
        }

        if (thokPulseProgress.isRunning)
        {
            opacity = Mathf.Max(opacity, 1f - thokPulseProgress.progress);
        }

        trails.startColor = new Color(trails.startColor.r, trails.startColor.g, trails.startColor.b, opacity);
        trails.emitting = opacity >= 0.05f;
    }
}
