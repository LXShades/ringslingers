using UnityEngine;

public class DamageOnTouch : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out Damageable damageable))
        {
            damageable.TryDamage(gameObject);
        }
    }
}
