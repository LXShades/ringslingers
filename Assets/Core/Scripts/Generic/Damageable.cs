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
            RpcDamage(instigator);

            if (!NetworkClient.active) // no client active? we need to play the RPC ourself...
                OnDamage(instigator);
        }
    }

    [ClientRpc]
    private void RpcDamage(GameObject instigator)
    {
        OnDamage(instigator);
    }

    private void OnDamage(GameObject instigator)
    {
        invincibilityTimeRemaining = hitInvincibilityDuration;
        onDamaged?.Invoke(instigator);
    }
}
