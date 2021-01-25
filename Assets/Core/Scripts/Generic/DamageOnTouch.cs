using UnityEngine;

public class DamageOnTouch : MonoBehaviour
{
    public bool instaKill = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Damageable damageable))
        {
            damageable.TryDamage(gameObject, default, instaKill);
        }
    }
}
