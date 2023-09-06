using UnityEngine;

public class TrainingBotBase : MonoBehaviour
{
    [Header("Requirements")]
    public int[] layers = new int[3] { 5, 3, 2 };
    public NeuralNetwork network;
    public TrainingEnvironmentBase trainingEnvironmentPrefab;

    [Header("Save/Load")]
    public string networkName = "";
    public NeuralTrainer trainer { get; set; }

    [Header("Simulation")]
    float maxDeltaTime = 0.033f;

    public PlayerCharacterMovement movement { get; private set; }
    public PlayerCharacterMovement localPlayer { get; set; }
    public TrainingEnvironmentBase trainingEnvironment { get; set; }

    protected float[] networkInput;

    protected Vector3 startPosition;
    protected Quaternion startRotation;

    private void Awake()
    {
        movement = GetComponent<PlayerCharacterMovement>();
        networkInput = new float[layers[0]];
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    protected virtual void Start()
    {
    }

    protected virtual void Update()
    {
        for (float t = 0; t < Time.deltaTime; t += maxDeltaTime)
        {
            CharacterInput charInput = default;
            float dt = Mathf.Min(Time.deltaTime - t, maxDeltaTime);

            if (OnTick(dt, t, ref charInput))
                movement.TickMovement(dt, charInput, new TickInfo() { isFullTick = true, isForwardTick = false });
            else
                break;
        }
    }

    public virtual bool OnTick(float deltaTime, float substepBase, ref CharacterInput charInput)
    {
        charInput = default;
        return true;
    }

    public virtual float GetFitness() => 1f;

    public void Reset()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
        OnReset();
    }

    public virtual void OnReset() { }
}