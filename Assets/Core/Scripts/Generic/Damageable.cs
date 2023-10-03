using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class Damageable : NetworkBehaviour
{
    /// <summary>
    /// GameObject instigator, Vector3 force, bool instaKill
    /// </summary>
    public UnityEvent<GameObject, Vector3, bool> onLocalDamaged;

    [Header("I-frames")]
    public float hitInvincibilityDuration = 1.5f;

    public bool doInvincibilityBlink = true;
    public float hitInvincibilityBlinkRate = 25f;
    public float invincibilityTimeRemaining { get; private set; }

    [Header("Visuals")]
    public bool autoPopulateRenderers = true;
    public Renderer[] affectedRenderers = new Renderer[0];

    [Header("Teams")]
    [Tooltip("Except when 0 (neutral), objects with the same damage team won't hurt each other")]
    public int damageTeam = 0;

    public bool isInvincible => invincibilityTimeRemaining > 0f;

    private void Update()
    {
        // Invincibility blinky
        if (invincibilityTimeRemaining > 0)
        {
            invincibilityTimeRemaining = Mathf.Max(invincibilityTimeRemaining - Time.deltaTime, 0);

            if (doInvincibilityBlink)
            {
                bool enableRenderers;

                if (invincibilityTimeRemaining > 0)
                    enableRenderers = ((int)(Time.time * hitInvincibilityBlinkRate) & 1) == 0;
                else
                    enableRenderers = true; // we finished blinky blinkying

                foreach (Renderer renderer in affectedRenderers)
                    renderer.enabled = enableRenderers;
            }
        }
    }

    public bool CanBeDamagedBy(int otherDamageTeam)
    {
        return otherDamageTeam == 0 || this.damageTeam == 0 || otherDamageTeam != this.damageTeam;
    }

    public bool TryDamage(GameObject instigator, Vector3 force = default, bool instaKill = false)
    {
        if (instigator && instigator.TryGetComponent(out Damageable instigatorDamageable))
        {
            if (!CanBeDamagedBy(instigatorDamageable.damageTeam))
                return false; // hit by someone on the same team
        }

        if (invincibilityTimeRemaining <= 0f || instaKill)
        {
            OnDamage(instigator, force, instaKill);
            return true;
        }
        return false;
    }

    [ClientRpc]
    private void RpcStartInvincibilityTime(float duration)
    {
        invincibilityTimeRemaining = duration;
    }

    private void OnDamage(GameObject instigator, Vector3 force, bool instaKill)
    {
        if (NetworkServer.active)
        {
            invincibilityTimeRemaining = hitInvincibilityDuration;
            RpcStartInvincibilityTime(hitInvincibilityDuration);
        }
        onLocalDamaged?.Invoke(instigator, force, instaKill);
    }

    private void OnValidate()
    {
        if (autoPopulateRenderers)
            affectedRenderers = GetComponentsInChildren<Renderer>();
    }
}
