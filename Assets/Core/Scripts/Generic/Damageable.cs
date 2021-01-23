using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Damageable : NetworkBehaviour
{
    public UnityEvent<GameObject, Vector3> onLocalDamaged;

    [Header("I-frames")]
    public float hitInvincibilityDuration = 1.5f;

    public bool doInvincibilityBlink = true;
    public float hitInvincibilityBlinkRate = 25f;
    private float invincibilityTimeRemaining;

    public bool autoPopulateRenderers = true;
    public Renderer[] affectedRenderers = new Renderer[0];

    public bool isInvincible => invincibilityTimeRemaining > 0f;

    private void Update()
    {
        // Invincibility blinky
        if (invincibilityTimeRemaining > 0)
        {
            invincibilityTimeRemaining = Mathf.Max(invincibilityTimeRemaining - Time.deltaTime, 0);

            bool enableRenderers;

            if (invincibilityTimeRemaining > 0)
                enableRenderers = ((int)(Time.time * hitInvincibilityBlinkRate) & 1) == 0;
            else
                enableRenderers = true; // we finished blinky blinkying

            foreach (Renderer renderer in affectedRenderers)
                renderer.enabled = enableRenderers;
        }
    }

    public void TryDamage(GameObject instigator, Vector3 force = default)
    {
        if (invincibilityTimeRemaining <= 0f)
        {
            OnDamage(instigator, force);
        }
    }

    // currently unused
    [ClientRpc]
    private void RpcDamage(GameObject instigator, Vector3 force)
    {
        OnDamage(instigator, force);
    }

    private void OnDamage(GameObject instigator, Vector3 force)
    {
        invincibilityTimeRemaining = hitInvincibilityDuration;
        onLocalDamaged?.Invoke(instigator, force);
    }
    private void OnValidate()
    {
        if (autoPopulateRenderers)
            affectedRenderers = GetComponentsInChildren<Renderer>();
    }
}
