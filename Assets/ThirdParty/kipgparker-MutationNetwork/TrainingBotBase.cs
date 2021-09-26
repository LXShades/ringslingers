using System.Collections.Generic;
using UnityEngine;

public class TrainingBotBase : MonoBehaviour
{
    [Header("Save/Load")]
    public string networkName = "";

    [Header("Neural Network Requirements")]
    public int[] layers = new int[3] { 5, 3, 2 };
    public NeuralNetwork network;
    public NeuralTrainer trainer { get; set; }

    [Header("Simulation")]
    float maxDeltaTime = 0.033f;

    public Transform target;
    public Transform nextTarget;
    public PlayerCharacterMovement movement { get; private set; }
    public List<Transform> checkpoints;
    public PlayerCharacterMovement localPlayer;

    protected float[] networkInput;

    protected Vector3 startPosition;
    protected Quaternion startRotation;

    public float checkpointRadius;
    public int currentCheckpoint = 0;

    private void Awake()
    {
        movement = GetComponent<PlayerCharacterMovement>();
        networkInput = new float[layers[0]];
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    protected virtual void Start()
    {
        target = checkpoints.Count > 0 ? checkpoints[0] : null;
        nextTarget = checkpoints.Count > 1 ? checkpoints[1] : null;
    }

    protected virtual void Update()
    {
        for (float t = 0; t < Time.deltaTime; t += maxDeltaTime)
        {
            PlayerInput charInput = default;
            float dt = Mathf.Min(Time.deltaTime - t, maxDeltaTime);

            if (OnTick(dt, t, ref charInput))
                movement.TickMovement(dt, charInput, false);
            else
                break;
        }
    }

    public virtual bool OnTick(float deltaTime, float substepBase, ref PlayerInput charInput)
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

    private void UpdateAsOnGround(ref PlayerInput charInput)
    {
        networkInput[0] = target.position.x - transform.position.x;
        networkInput[1] = target.position.z - transform.position.z;
        networkInput[2] = movement.velocity.x;
        networkInput[3] = movement.velocity.z;

        float[] output = network.FeedForward(networkInput);
        Vector2 dir = new Vector2(output[0], output[1]).normalized;

        charInput.moveHorizontalAxis = dir.x;
        charInput.moveVerticalAxis = dir.y;
    }
}