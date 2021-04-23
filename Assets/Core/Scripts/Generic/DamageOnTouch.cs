using Mirror;
using UnityEngine;

public class DamageOnTouch : MonoBehaviour, IMovementCollisions
{
    public bool instaKill = false;
    public int team;
    public float knockback = 0f;
    public GameObject owner;

    [Tooltip("Message to post when damaged. The message will be posted in the format '[playername] [hitMessage]'")]
    public string hitMessage;

    public void OnMovementCollidedBy(Movement source, bool isReconciliation)
    {
        if (source.TryGetComponent(out TheFlag flag))
        {
            // flag should respawn if hitting death zone
            if (Mirror.NetworkServer.active)
                flag.ReturnToBase(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Damageable damageable) && damageable.CanBeDamagedBy(team) && damageable.gameObject != owner)
        {
            Vector3 force = knockback > 0 ? (other.transform.position - transform.position).Horizontal().normalized * knockback : default;
            
            if (damageable.TryDamage(owner, force, instaKill))
            {
                if (!string.IsNullOrEmpty(hitMessage) && NetworkServer.active && damageable.TryGetComponent(out Character player))
                {
                    MessageFeed.Post($"<player>{player.playerName}</player> {hitMessage}");
                }
            }
        }
    }
}
