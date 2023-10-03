using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Turret : MonoBehaviour
{
    public float shotsPerSecond = 4;
    public float range = 10f;

    public Transform projectileSpawnPoint;
    public ThrownRing projectilePrefab;

    private float nextShotTime = 0f;

    private void Update()
    {
        if (!NetworkServer.active)
            return;
        if (shotsPerSecond <= 0)
            return;

        nextShotTime -= Time.deltaTime;

        if (nextShotTime <= 0)
        {
            nextShotTime += 1f / shotsPerSecond;

            Shoot();
        }
    }

    private void Shoot()
    {
        float closestCharacterDistance = range;
        Character closestCharacter = null;

        // find closest character and shoot em
        Vector3 selfPosition = projectileSpawnPoint.position;
        foreach (Character character in Netplay.singleton.players)
        {
            float distanceToCharacter = Vector3.Distance(selfPosition, character.transform.position);
            if (character && distanceToCharacter < closestCharacterDistance)
            {
                closestCharacterDistance = distanceToCharacter;
                closestCharacter = character;
            }
        }

        if (closestCharacter != null)
        {
            GameObject go = Spawner.Spawn(projectilePrefab.gameObject);

            go.GetComponent<ThrownRing>().Throw(null, selfPosition, closestCharacter.GetTorsoPosition() - selfPosition, 0f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
