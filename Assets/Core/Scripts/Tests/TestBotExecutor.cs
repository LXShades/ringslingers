using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class TestBotExecutor : MonoBehaviour
{
    public enum DotDisplayType
    {
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

    [Serializable]
    public struct TurnCalculator
    {
        public float inputCurrentSpeed;
        public float inputRelativeAccelAngle;
        public float accelerationAtTurnSpeed;
    }

    [Header("Time")]
    [Range(0f, 50f)]
    public float simulationDuration;
    public float deltaTime = 0.0166666f;

    [Header("Action")]
    public Vector3 startVelocity;
    [SerializeReference]
    public TestBotAction actionToPerform = new TestBotAction_RunToPoints();

    [Header("Targets")]
    public float targetRadius = 0.5f;
    public List<Vector3> targetPositions = new List<Vector3>();

    public int currentTargetIndex { get; set; } = 0;
    private float currentTime;

    [Header("Run")]
    public bool runNow;
    public bool runConstantly;
    private bool isSimulationRunning;
    public bool useFullSimulation = true;

    [Header("Display")]
    public DotDisplayType dotDisplayType = DotDisplayType.FadePerSecond;
    public StateSnapshot watchState;
    [Range(0f, 5f)]
    public float stateTime;
    public bool autoplay;

    [Header("Calculators")]
    public TurnCalculator turnCalculator;

    [Header("Output")]
    public float timeTaken;

    public PlayerCharacterMovement movement;
    private CharacterInput input = default;

    private List<Tuple<Vector3, Quaternion>> positionHistory = new List<Tuple<Vector3, Quaternion>>();

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
        }
    }

    private void Run()
    {
        movement = GetComponent<PlayerCharacterMovement>();
        positionHistory.Clear();

        currentTime = 0f;
        currentTargetIndex = 0;

        float turnAccelerationDot = Mathf.Cos(turnCalculator.inputRelativeAccelAngle * Mathf.Deg2Rad);
        float frictionMagnitude = turnCalculator.inputCurrentSpeed - (PlayerCharacterMovement.CalculateFrictionMultiplier(movement.friction, deltaTime) * turnCalculator.inputCurrentSpeed);
        float accelerationGeneral = PlayerCharacterMovement.GetAccelerationMagnitude(turnCalculator.inputCurrentSpeed, movement.accelCurve, movement.inverseAccelCurve, deltaTime);
        turnCalculator = new TurnCalculator()
        {
            inputCurrentSpeed = turnCalculator.inputCurrentSpeed,
            inputRelativeAccelAngle = turnCalculator.inputRelativeAccelAngle,
            accelerationAtTurnSpeed = accelerationGeneral * turnAccelerationDot - frictionMagnitude
        };

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

            actionToPerform.Init(this);

            Vector3 lastVelocity = initialState.velocity;
            movement.velocity = initialState.velocity;

            for (currentTime = 0f; currentTime < simulationDuration && isSimulationRunning; currentTime += deltaTime)
            {
                if (currentTime <= stateTime && currentTime + deltaTime > stateTime)
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
        actionToPerform.Run(this, ref input);

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

        if (currentTargetIndex < targetPositions.Count && Vector3.Distance(transform.position, targetPositions[currentTargetIndex]) <= targetRadius)
        {
            currentTargetIndex++;

            if (currentTargetIndex >= targetPositions.Count)
                FinishSimulation();
        }
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
            Gizmos.DrawSphere(positionHistory[i].Item1, 0.25f);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(watchState.position, watchState.position + watchState.velocity.normalized);
        Gizmos.DrawSphere(watchState.position + watchState.velocity.normalized, 0.05f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(watchState.position, watchState.position + watchState.inputDirection.normalized);
        Gizmos.DrawSphere(watchState.position + watchState.inputDirection.normalized, 0.05f);

        actionToPerform.OnDrawGizmos();
    }
}

[System.Serializable]
public class TestBotAction
{
    public virtual void Init(TestBotExecutor exec) { }
    public virtual void Run(TestBotExecutor exec, ref CharacterInput input) { }

    public virtual void OnDrawGizmos() { }
}