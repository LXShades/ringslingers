using UnityEngine;

[RequireComponent(typeof(Visibility))]
public class Blinker : MonoBehaviour
{
    public float timeRemaining { get; set; }

    [Header("Visuals")]
    public AnimationCurve blinkRateOverTimeRemaining = AnimationCurve.Linear(0, 10, 5, 0);

    private float perBlinkTimer = 0;
    private bool blinkRenderEnabled = true;

    private Visibility visibility;

    private void Awake()
    {
        visibility = GetComponent<Visibility>();
    }

    void Update()
    {
        if (timeRemaining > 0f)
        {
            // Countdown!
            timeRemaining = Mathf.Max(timeRemaining - Time.deltaTime, 0f);

            // Blink!
            float rate = blinkRateOverTimeRemaining.Evaluate(timeRemaining);

            perBlinkTimer += Time.deltaTime;

            if (rate > 0 && timeRemaining > 0f)
            {
                if (perBlinkTimer >= 1f / rate)
                {
                    blinkRenderEnabled = !blinkRenderEnabled;
                    visibility.Set(this, blinkRenderEnabled);
                    perBlinkTimer = 0f;
                }
            }
            else
            {
                blinkRenderEnabled = true;
                visibility.Set(this, blinkRenderEnabled);
            }
        }
        else
        {
            // blinking was stopped externally but we're still invisible, fix that
            Stop();
        }
    }

    public void Stop()
    {
        timeRemaining = 0f;
        blinkRenderEnabled = true;
        visibility.Unset(this);
    }
}
