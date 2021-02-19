using Mirror;
using UnityEngine;

public class SpawnObject : MonoBehaviour
{
    public GameObject prefabToSpawn;

    public bool canSpawnLocally = false;

    public void Spawn(GameObject source)
    {
        if (NetworkServer.active || canSpawnLocally)
        {
            GameObject obj = Spawner.Spawn(prefabToSpawn, transform.position, Quaternion.identity);
            int sourceTeam = 0;

            if (source.TryGetComponent(out ThrownRing thrownRing))
                sourceTeam = thrownRing.owner?.GetComponent<Damageable>()?.damageTeam ?? 0;

            if (obj.TryGetComponent(out DamageOnTouch damager))
            {
                damager.owner = source;
                damager.team = sourceTeam;
            }
        }
    }
}
