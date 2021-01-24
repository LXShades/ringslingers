using UnityEngine;

public class DespawnAfterDuration : MonoBehaviour
{
    [Header("Despawn timing")]
    public float timeUntilDespawn = 1f;

    [Header("Blinking")]
    public Renderer[] affectedRenderers = new Renderer[0];
    public AnimationCurve blinkRateOverTime = AnimationCurve.Linear(0, 0, 1, 10);

    private float despawnTimeRemaining;
    private float perBlinkTimer = 0;

    [Header("Sounds")]
    public GameSound despawnSound = new GameSound();

    void Awake()
    {
        despawnTimeRemaining = timeUntilDespawn;
    }

    void Update()
    {
        // Countdown!
        despawnTimeRemaining -= Time.deltaTime;

        // Blink!
        if (affectedRenderers != null && affectedRenderers.Length > 0)
        {
            float rate = blinkRateOverTime.Evaluate(timeUntilDespawn - despawnTimeRemaining);

            perBlinkTimer += Time.deltaTime;

            foreach (Renderer renderer in affectedRenderers)
            {
                if (rate > 0)
                {
                    if (perBlinkTimer >= 1f / rate)
                    {
                        renderer.enabled = !renderer.enabled;
                        perBlinkTimer = 0f;
                    }
                }
                else
                {
                    renderer.enabled = true;
                }
            }
        }

        // Destroy! (eventually)
        if (despawnTimeRemaining <= 0)
        {
            if (despawnSound.clip)
                GameSounds.PlaySound(gameObject, despawnSound);

            Spawner.Despawn(gameObject);
        }
    }
}
