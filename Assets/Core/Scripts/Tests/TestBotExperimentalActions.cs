using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class TestBotAction_RunToPoints : TestBotAction
{
    public override void Run(TestBotExecutor exec, ref CharacterInput input)
    {
        Vector3 targetPosition = exec.targetPositions[exec.currentTargetIndex];
        Vector3 moveIntentionDirection = targetPosition - exec.transform.position;
        Vector3 intendedAim = Vector3.forward;
        input = new CharacterInput()
        {
            aimDirection = intendedAim,
        };

        input.worldMovementDirection = moveIntentionDirection;
    }
}

[System.Serializable]
public class TestBotAction_RunToPointThenForceDirection : TestBotAction
{
    [Range(0, 360)]
    public float directionToForce = 0f;
    public float magnitude = 1f;

    private Vector3 preTurnDirection;

    public override void Run(TestBotExecutor exec, ref CharacterInput input)
    {
        Vector3 intendedAim = Vector3.forward;
        input = new CharacterInput()
        {
            aimDirection = intendedAim,
        };

        if (exec.currentTargetIndex == 0)
        {
            Vector3 targetPosition = exec.targetPositions[exec.currentTargetIndex];
            Vector3 moveIntentionDirection = targetPosition - exec.transform.position;

            input.worldMovementDirection = moveIntentionDirection;
            preTurnDirection = moveIntentionDirection.normalized;
        }
        else
        {
            float dirRad = directionToForce * Mathf.Deg2Rad;
            input.worldMovementDirection = new Vector3(Mathf.Cos(dirRad) * preTurnDirection.x - Mathf.Sin(dirRad) * preTurnDirection.z, 0f, Mathf.Sin(dirRad) * preTurnDirection.x + Mathf.Cos(dirRad) * preTurnDirection.z) * magnitude;
        }
    }
}

[System.Serializable]
public class TestBotAction_RunToPointThenCircle : TestBotAction
{
    [Range(0, 360)]
    public float circleDegreesPerSecond = 0f;
    [Range(0f, 5f)]
    public float circleDuration = 1f;

    private Vector3 preCircleDirection;
    private float currentCircleRad = 0f;
    private float currentCircleTime = 0f;

    public override void Run(TestBotExecutor exec, ref CharacterInput input)
    {
        Vector3 intendedAim = Vector3.forward;
        input = new CharacterInput()
        {
            aimDirection = intendedAim,
        };

        if (exec.currentTargetIndex == 0)
        {
            Vector3 targetPosition = exec.targetPositions[exec.currentTargetIndex];
            Vector3 moveIntentionDirection = targetPosition - exec.transform.position;

            input.worldMovementDirection = moveIntentionDirection;
            preCircleDirection = moveIntentionDirection.normalized;
            currentCircleRad = 0f;
            currentCircleTime = 0f;
        }
        else
        {
            input.worldMovementDirection = new Vector3(
                preCircleDirection.x * Mathf.Cos(currentCircleRad) - preCircleDirection.z * Mathf.Sin(currentCircleRad), 0f,
                preCircleDirection.x * Mathf.Sin(currentCircleRad) + preCircleDirection.z * Mathf.Cos(currentCircleRad));

            if (currentCircleTime < circleDuration)
            {
                currentCircleRad = (currentCircleRad + (exec.deltaTime * circleDegreesPerSecond * Mathf.Deg2Rad)) % (Mathf.PI * 2);
                currentCircleTime += exec.deltaTime;
            }
        }
    }
}

[System.Serializable]
public class TestBotAction_BeelineWithLookahead : TestBotAction_WithPreCalculatedPath
{
    public int refinementIterations = 0;

    public float lookAheadTime = 0f;
    
    public override void CalculatePathPoint(TestBotExecutor exec, ref CharacterInput input, float t, in CharacterState state, int currentTarget)
    {
        Vector3 lookaheadPosition = state.position + state.velocity * lookAheadTime;

        input = new CharacterInput() { worldMovementDirection = exec.targetPositions[currentTarget] - lookaheadPosition };
    }
}

public class TestBotAction_WithPreCalculatedPath : TestBotAction
{
    public float inputInterval = 0.2f;

    private List<CharacterInput> inputs = new List<CharacterInput>();

    private int playbackInputFrame = 0;

    public override void Init(TestBotExecutor exec)
    {
        playbackInputFrame = 0;
        inputs.Clear();

        int currentTarget = 0;
        CharacterState state = new CharacterState() { position = exec.transform.position, velocity = exec.startVelocity };
        CharacterInput input = default;
        float lastInputChangeTime = -inputInterval - 1f;

        for (float t = 0f; t < exec.simulationDuration; t += exec.deltaTime)
        {
            if (currentTarget >= exec.targetPositions.Count)
            {
                break;
            }

            if (t - lastInputChangeTime >= inputInterval)
            {
                CalculatePathPoint(exec, ref input, t, in state, currentTarget);
                lastInputChangeTime = t;
            }

            inputs.Add(input);
            exec.movement.RunSimpleCollisionFreeSimulation(ref state, input, exec.deltaTime);

            if (VectorExtensions.HorizontalDistance(state.position, exec.targetPositions[currentTarget]) < exec.targetRadius)
                currentTarget++;
        }
    }

    public override void Run(TestBotExecutor exec, ref CharacterInput input)
    {
        if (playbackInputFrame < inputs.Count)
            input = inputs[playbackInputFrame++];
    }

    public virtual void CalculatePathPoint(TestBotExecutor exec, ref CharacterInput input, float t, in CharacterState state, int currentTarget) { }
}