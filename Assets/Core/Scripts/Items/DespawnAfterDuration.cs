using UnityEngine;

public class DespawnAfterDuration : MonoBehaviour
{
    [Header("Despawn timing")]
    public float timeUntilDespawn = 1f;

    [Header("Blinking")]
    public AnimationCurve blinkRateOverTime = AnimationCurve.Linear(0, 0, 1, 10);

    private float despawnTimeRemaining;
    private float perBlinkTimer = 0;

    [Header("Sounds")]
    public GameSound despawnSound = new GameSound();

    // Components
    private Renderer myRenderer;

    void Awake()
    {
        despawnTimeRemaining = timeUntilDespawn;
        myRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        // Countdown!
        despawnTimeRemaining -= Time.deltaTime;

        // Blink!
        if (myRenderer)
        {
            float rate = blinkRateOverTime.Evaluate(timeUntilDespawn - despawnTimeRemaining);

            perBlinkTimer += Time.deltaTime;

            if (rate > 0)
            {
                if (perBlinkTimer >= 1f / rate)
                {
                    myRenderer.enabled = !myRenderer.enabled;
                    perBlinkTimer = 0f;
                }
            }
            else
            {
                myRenderer.enabled = true;
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
