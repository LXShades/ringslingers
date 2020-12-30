using UnityEngine;

public class Predictable : MonoBehaviour
{
    public bool isPrediction { get; set; }

    public bool wasPredicted { get; set; }

    public float spawnTime { get; set; }

    public System.Action onPredictionSuccessful;

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
