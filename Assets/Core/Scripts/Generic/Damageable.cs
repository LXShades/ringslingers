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

    private void Update()
    {
        // Invincibility blinky
        if (invincibilityTimeRemaining > 0)
        {
            invincibilityTimeRemaining = Mathf.Max(invincibilityTimeRemaining - Time.deltaTime, 0);

            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer && invincibilityTimeRemaining > 0)
                renderer.enabled = ((int)(Time.time * hitInvincibilityBlinkRate) & 1) == 0;
            else
                renderer.enabled = true; // we finished blinky blinkying
        }
    }

    public void TryDamage(GameObject instigator, Vector3 force = default)
    {
        OnDamage(instigator, force);
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
}
