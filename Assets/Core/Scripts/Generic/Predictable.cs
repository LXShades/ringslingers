using UnityEngine;

public class Predictable : MonoBehaviour
{
    public bool isPrediction { get; set; }

    public float spawnTime { get; set; }

    [Tooltip("How many seconds until this predictable object expires, if not replaced")]
    public float expiryTime = 0.5f;

    private void Awake()
    {
        spawnTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (isPrediction && Time.unscaledTime - spawnTime > expiryTime)
        {
            Log.Write($"Prediction \"{gameObject}\" expired, bye!");
            Spawner.Despawn(gameObject);
        }
    }
}
