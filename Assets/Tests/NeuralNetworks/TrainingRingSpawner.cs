using System.Collections.Generic;
using UnityEngine;

public class TrainingRingSpawner : MonoBehaviour
{
    public float spawnRate = 100f;
    public float fireSpeed = 32.81f;
    public float ringRange = 60f;

    private NeuralTrainer trainer;

    public struct Ring
    {
        public Vector3 position;
        public Vector3 speed;
    }
    public List<Ring> spawnedRings { get; private set; } = new List<Ring>();

    private void Awake()
    {
        trainer = FindObjectOfType<NeuralTrainer>();
    }

    private void Start()
    {
        trainer.onCycle += OnCycle;
    }

    private void OnCycle()
    {
        spawnedRings.Clear();

        //for (int i = (int)(Time.time * spawnRate) - (int)((Time.time - Time.deltaTime) * spawnRate); i > 0; i--)
        for (int i = 0; i < spawnRate; i++)
        {
            Vector3 speed = Random.insideUnitCircle.normalized;
            speed.z = speed.y;
            speed.y = 0;
            Vector3 position = transform.position + new Vector3(Random.Range(-ringRange, ringRange), 0, Random.Range(-ringRange, ringRange));
            spawnedRings.Add(new Ring() { position = position, speed = speed * fireSpeed });
        }
        
        for (int i = 0; i < spawnedRings.Count; i++)
            spawnedRings[i] = new Ring() { position = spawnedRings[i].position + spawnedRings[i].speed * Time.deltaTime, speed = spawnedRings[i].speed };

        for (int i = 0; i < spawnedRings.Count; i++)
        {
            if (Vector3.Distance(spawnedRings[i].position, transform.position) >= ringRange)
                spawnedRings.RemoveAt(i--);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        for (int i = 0; i < spawnedRings.Count; i++)
        {
            Gizmos.DrawSphere(spawnedRings[i].position, 0.25f);
        }
    }
}
