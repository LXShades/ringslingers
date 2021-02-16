using Mirror;
using UnityEngine;

public class SpawnObject : MonoBehaviour
{
    public GameObject prefabToSpawn;

    public void SpawnAtOrigin()
    {
        if (NetworkServer.active)
        {
            Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
        }
    }
}
