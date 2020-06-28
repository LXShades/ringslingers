using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DespawnAfterDuration : SyncedObject
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
    private Renderer renderer;

    public override void FrameAwake()
    {
        despawnTimeRemaining = timeUntilDespawn;
        renderer = GetComponent<Renderer>();
    }

    public override void FrameUpdate()
    {
        // Countdown!
        despawnTimeRemaining -= Frame.local.deltaTime;

        // Blink!
        if (renderer)
        {
            float rate = blinkRateOverTime.Evaluate(timeUntilDespawn - despawnTimeRemaining);

            perBlinkTimer += Frame.local.deltaTime;

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

        // Destroy! (eventually)
        if (despawnTimeRemaining <= 0)
        {
            if (despawnSound.clip)
                GameSounds.PlaySound(gameObject, despawnSound);

            Destroy(gameObject);
        }
    }
}
