using UnityEngine;

public class Blinker : MonoBehaviour
{
    public float timeRemaining { get; set; }

    [Header("Visuals")]
    public bool autoPopulateRenderers = true;
    public Renderer[] affectedRenderers = new Renderer[0];
    public AnimationCurve blinkRateOverTimeRemaining = AnimationCurve.Linear(0, 10, 5, 0);

    private float perBlinkTimer = 0;
    private bool blinkRenderEnabled = true;

    void Update()
    {
        if (timeRemaining > 0f)
        {
            // Countdown!
            timeRemaining = Mathf.Max(timeRemaining - Time.deltaTime, 0f);

            // Blink!
            if (affectedRenderers != null && affectedRenderers.Length > 0)
            {
                float rate = blinkRateOverTimeRemaining.Evaluate(timeRemaining);

                perBlinkTimer += Time.deltaTime;

                if (rate > 0 && timeRemaining > 0f)
                {
                    if (perBlinkTimer >= 1f / rate)
                    {
                        blinkRenderEnabled = !blinkRenderEnabled;
                        perBlinkTimer = 0f;
                    }
                }
                else
                {
                    blinkRenderEnabled = true;
                }

                foreach (Renderer renderer in affectedRenderers)
                {
                    renderer.enabled = blinkRenderEnabled;
                }
            }
        }
        else if (!blinkRenderEnabled)
        {
            // blinking was stopped externally but we're still invisible, fix that
            blinkRenderEnabled = true;

            foreach (Renderer renderer in affectedRenderers)
            {
                renderer.enabled = blinkRenderEnabled;
            }
        }
    }

    private void OnValidate()
    {
        if (autoPopulateRenderers)
        {
            affectedRenderers = GetComponentsInChildren<Renderer>();
        }
    }
}
