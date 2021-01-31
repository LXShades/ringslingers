using UnityEngine;

public class DamageOnTouch : MonoBehaviour, IMovementCollisions
{
    public bool instaKill = false;

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
        if (other.TryGetComponent(out Damageable damageable))
        {
            damageable.TryDamage(gameObject, default, instaKill);
        }
    }
}
