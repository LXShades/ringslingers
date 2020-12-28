using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Damageable : NetworkBehaviour
{
    public UnityEvent<GameObject> onDamaged;

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

    public void TryDamage(GameObject instigator)
    {
        if (NetworkServer.active && invincibilityTimeRemaining <= 0f)
        {
            invincibilityTimeRemaining = hitInvincibilityDuration;

            RpcDamage(instigator);

            if (!NetworkClient.active) // boo, no client, server won't receive Rpcs
                onDamaged?.Invoke(instigator);
        }
    }

    [ClientRpc]
    private void RpcDamage(GameObject instigator)
    {
        onDamaged?.Invoke(instigator);
    }
}
