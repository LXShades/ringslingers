using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DespawnAfterDuration : WorldObjectComponent
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

    public override void WorldAwake()
    {
        despawnTimeRemaining = timeUntilDespawn;
        myRenderer = GetComponent<Renderer>();
    }

    public override void WorldUpdate(float deltaTime)
    {
        // Countdown!
        despawnTimeRemaining -= World.live.deltaTime;

        // Blink!
        if (myRenderer)
        {
            float rate = blinkRateOverTime.Evaluate(timeUntilDespawn - despawnTimeRemaining);

            perBlinkTimer += deltaTime;

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

            World.Despawn(gameObject);
        }
    }
}
