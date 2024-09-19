using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public struct CharacterMovementTurnCalculator
{
    public float inputCurrentSpeed;
    public float inputRelativeAccelAngle;
    public float accelerationAtTurnSpeed;

    public void Calculate(PlayerCharacterMovement movement, float deltaTime)
    {
        float turnAccelerationDot = Mathf.Cos(inputRelativeAccelAngle * Mathf.Deg2Rad);
        float frictionMagnitude = inputCurrentSpeed - (PlayerCharacterMovement.CalculateFrictionMultiplier(movement.friction, deltaTime) * inputCurrentSpeed);
        float accelerationGeneral = PlayerCharacterMovement.GetAccelerationMagnitude(inputCurrentSpeed, movement.accelCurve, movement.inverseAccelCurve, deltaTime);
        accelerationAtTurnSpeed = accelerationGeneral * turnAccelerationDot - frictionMagnitude;
    }
}

[ExecuteInEditMode]
public class TestBotExecutor : MonoBehaviour
{
    public enum DotDisplayType
    {
        None,
        FadePerSecond,
        Speed,
        Acceleration
    }

    [Serializable]
    public struct StateSnapshot
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
        public Vector3 inputDirection;
        public float velocityMagnitude;
        public float accelerationMagnitude;
    }

    [Header("Time")]
    [Range(0f, 50f)]
    public float simulationDuration;
    public float deltaTime = 0.0166666f;

    [Header("Action")]
    public Vector3 startVelocity;
    [SerializeReference, PolymorphicTypeSelector]
    public ITestableBotTask actionToPerform = new BT_BeelinePathFollower();

    [Header("Targets")]
    public List<Vector3> targetPositions = new List<Vector3>();

    /// <summary>
    /// DEPRECATED - no longer valid, some code needs updating. Use BT_PathFollower(s) instead
    /// </summary>
    public int currentTargetIndex { get; set; } = 0;
    private float currentTime;

    [Header("Run")]
    public bool runNow;
    public bool runConstantly;
    private bool isSimulationRunning;
    public bool useFullSimulation = true;
    public bool spawnBotHereOnGameStart = true;

    [Header("Display")]
    public DotDisplayType dotDisplayType = DotDisplayType.FadePerSecond;
    public float dotDisplaySize = 0.25f;
    public StateSnapshot watchState;
    [Range(0f, 15f)]
    public float watchTime;
    public bool autoplay;

    [Header("Calculators")]
    public CharacterMovementTurnCalculator turnCalculator;

    [Header("Output")]
    public float timeTaken;

    public PlayerCharacterMovement movement;
    private CharacterInput input = default;
    private CharacterInput lastInput = default;
    private bool isWatchTime;

    private List<Tuple<Vector3, Quaternion>> positionHistory = new List<Tuple<Vector3, Quaternion>>();

    private void Start()
    {
        if (Application.isPlaying)
        {
            gameObject.SetActive(false);
            if (spawnBotHereOnGameStart)
            {
                GameObject actualBot = Netplay.singleton.AddBot();
                Character character = Netplay.singleton.characters[actualBot.GetComponent<Player>().playerId];
                var state = character.MakeState();
                state.velocity = startVelocity;
                state.position = transform.position;
                character.ApplyState(state);
                character.entity.StoreCurrentState(character.entity.latestStateTime);
            }
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && deltaTime > 0f)
        {
            if (runNow)
            {
                runNow = false;
                Run();
            }

            if (runConstantly)
                Run();

            if (actionToPerform is IBotDebugDraws withGizmos)
                withGizmos.DrawDebugs();
        }
    }

    private void Run()
    {
        movement = GetComponent<PlayerCharacterMovement>();
        movement.fracunitsPerM = 64f;
        positionHistory.Clear();

        currentTime = 0f;
        currentTargetIndex = 0;

        turnCalculator.Calculate(movement, deltaTime);

        CharacterState initialState = new CharacterState()
        {
            position = transform.position,
            rotation = transform.rotation,
            state = movement.state,
            velocity = startVelocity, 
            up = movement.up,
            stateFloat = movement.stateFloat
        };

        try
        {
            isSimulationRunning = true;

            lastInput = default;

            actionToPerform.InitTests(this);
            if (actionToPerform is IBotTask botTask)
                botTask.Init(new BotTaskParams() { character = null, characterObject = gameObject, controller = null, deltaTime = deltaTime, movement = movement });

            Vector3 lastVelocity = initialState.velocity;
            movement.velocity = initialState.velocity;

            for (currentTime = 0f; currentTime < simulationDuration && isSimulationRunning; currentTime += deltaTime)
            {
                if (currentTime <= watchTime && currentTime + deltaTime > watchTime)
                {
                    watchState = new StateSnapshot()
                    {
                        velocity = movement.velocity,
                        position = transform.position,
                        velocityMagnitude = movement.velocity.magnitude,
                        acceleration = movement.velocity - lastVelocity,
                        accelerationMagnitude = movement.velocity.magnitude - lastVelocity.magnitude,
                        inputDirection = input.aimDirection * input.moveVerticalAxis + Vector3.Cross(Vector3.up, input.aimDirection).normalized * input.moveHorizontalAxis
                    };
                    isWatchTime = true;
                }
                else
                {
                    isWatchTime = false;
                }

                lastVelocity = movement.velocity;

                Simulate();
                positionHistory.Add(new Tuple<Vector3, Quaternion>(transform.position, transform.rotation));
            }
        }
        finally
        {
            transform.position = initialState.position;
            transform.rotation = initialState.rotation;
            movement.state = initialState.state;
            movement.velocity = initialState.velocity;
            movement.up = initialState.up;
            movement.stateFloat = initialState.stateFloat;
        }

        isSimulationRunning = false;
        timeTaken = currentTime;
    }

    private void Simulate()
    {
        input = default;
        if (actionToPerform is IBotTask botTask)
            botTask.Update(new BotTaskParams() { character = null, characterObject = gameObject, controller = null, deltaTime = deltaTime, movement = movement, isWatchTime = isWatchTime, lastInput = lastInput }, ref input);
        input = input.WithDeltas(lastInput);
        lastInput = input;

        if (useFullSimulation)
        {
            movement.TickMovement(deltaTime, input);
        }
        else
        {
            CharacterState state = new CharacterState() { position = transform.position, velocity = movement.velocity };
            movement.RunSimpleCollisionFreeSimulation(ref state, in input, deltaTime);
            transform.position = state.position;
            movement.velocity = state.velocity;
        }

        /*if (currentTargetIndex < targetPositions.Count && Vector3.Distance(transform.position, targetPositions[currentTargetIndex]) <= targetRadius)
        {
            currentTargetIndex++;

            if (currentTargetIndex >= targetPositions.Count)
                FinishSimulation();
        }*/
    }

    private void FinishSimulation()
    {
        isSimulationRunning = false;
    }

    private void OnDrawGizmos()
    {
        int numPositions = positionHistory.Count;
        float lastSpeed = 0f;
        float accelerationTolerance = 1f;
        if (dotDisplayType != DotDisplayType.None)
        {
            for (int i = 0; i < numPositions - 1; i++)
            {
                if (dotDisplayType == DotDisplayType.FadePerSecond)
                    Gizmos.color = Color.Lerp(new Color(0f, 0f, 0.5f), Color.red, (i * deltaTime) % 1f);
                else if (dotDisplayType == DotDisplayType.Speed)
                {
                    Gizmos.color = Color.Lerp(Color.red, Color.green, Vector3.Distance(positionHistory[i].Item1, positionHistory[i + 1].Item1) / deltaTime / movement.topSpeed);
                }
                else if (dotDisplayType == DotDisplayType.Acceleration)
                {
                    float speed = Vector3.Distance(positionHistory[i].Item1, positionHistory[i + 1].Item1) / deltaTime;
                    Gizmos.color = Color.Lerp(Color.red, Color.green, 0.5f + (speed - lastSpeed) / accelerationTolerance * 0.5f);
                    lastSpeed = speed;
                }
                Gizmos.DrawLine(positionHistory[i].Item1, positionHistory[i + 1].Item1);
                Gizmos.DrawSphere(positionHistory[i].Item1, dotDisplaySize);
            }
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(watchState.position, watchState.position + watchState.velocity.normalized);
        Gizmos.DrawSphere(watchState.position + watchState.velocity.normalized, 0.05f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(watchState.position, watchState.position + watchState.inputDirection.normalized);
        Gizmos.DrawSphere(watchState.position + watchState.inputDirection.normalized, 0.05f);
    }
}