using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TestBT_RunToPoints : IBotTask, ITestableBotTask
{
    TestBotExecutor executor;

    public void InitTests(TestBotExecutor exec)
    {
        executor = exec;
    }

    public void OnDrawGizmos()
    {
    }

    public void Init(in BotTaskParams taskParams)
    {
    }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        Vector3 targetPosition = executor.targetPositions[executor.currentTargetIndex];
        Vector3 moveIntentionDirection = targetPosition - taskParams.characterObject.transform.position;
        Vector3 intendedAim = Vector3.forward;
        input = new CharacterInput()
        {
            aimDirection = intendedAim,
        };

        input.worldMovementDirection = moveIntentionDirection;
    }
}

[System.Serializable]
public class TestBT_TimedTurns : IBotTask, ITestableBotTask
{
    [System.Serializable]
    public struct TimeTurnPair
    {
        public float time;
        public Vector2 movementDirection;
    }

    public bool wat;
    public List<TimeTurnPair> turns = new List<TimeTurnPair>();
    private float time;

    public void Init(in BotTaskParams taskParams) { time = 0f;  }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        time += taskParams.deltaTime;

        TimeTurnPair ok = default;
        for (int i = 0; i < turns.Count; i++)
        {
            if (time >= turns[i].time)
                ok = turns[i];
        }
        input.worldMovementDirection = new Vector3(ok.movementDirection.x, 0f, ok.movementDirection.y);
    }

    public void InitTests(TestBotExecutor exec) { }

    public void OnDrawGizmos() { }
}

[System.Serializable]
public class TestBT_GetOntoLine : IBotTask, ITestableBotTask
{
    private float time;

    private TestBotExecutor exec;

    private Vector3 lineStart;
    private Vector3 lineEnd;

    public AnimationCurve desiredSpeedByDistanceToLine = new AnimationCurve();

    public void Init(in BotTaskParams taskParams) { time = 0f; }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        Vector3 lineDirection = (lineEnd - lineStart).normalized;
        Vector3 velocityDirection = taskParams.movement.velocity.normalized;
        Vector3 position = taskParams.characterObject.transform.position;
        Vector3 toLine = (lineStart - lineDirection * Vector3.Dot(lineStart, lineDirection)) - (position - lineDirection * Vector3.Dot(position, lineDirection));
        float speedTowardsLine = Vector3.Dot(taskParams.movement.velocity, toLine.normalized);
        float targetSpeedTowardsLine = desiredSpeedByDistanceToLine.Evaluate(toLine.magnitude);

        if (speedTowardsLine > targetSpeedTowardsLine)
        {
            input.worldMovementDirection = lineDirection.normalized;
        }
        else
        {
            input.worldMovementDirection = toLine;
        }

        // TODO next: do a braking practice, make the character stop at the exact right point on the line
    }

    public void InitTests(TestBotExecutor exec)
    {
        this.exec = exec;
        if (exec.targetPositions.Count >= 2)
        {
            lineStart = exec.targetPositions[0];
            lineEnd = exec.targetPositions[1];
        }
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawLine(lineStart, lineEnd);
    }
}

[System.Serializable]
public class TestBT_SteerForDesiredVelocity : IBotTask, ITestableBotTask
{
    private TestBotExecutor exec;

    public float interval = 0.2f;

    private float time;

    public void InitTests(TestBotExecutor exec)
    {
        this.exec = exec;
        time = 0f;
    }

    public void Init(in BotTaskParams taskParams) { }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        time += taskParams.deltaTime;

        Vector3 naturalPositionAfterInterval = taskParams.position + taskParams.movement.velocity * interval;
        Vector3 desiredPosition = taskParams.position + (exec.targetPositions[exec.currentTargetIndex] - taskParams.position).normalized * (taskParams.movement.velocity.magnitude * interval);
        Vector3 accelerationPossibility = (desiredPosition - naturalPositionAfterInterval).normalized * (taskParams.movement.CalculateAccelerationMagnitude(taskParams.movement.velocity, time) * interval);

        if (time <= exec.watchTime && time + taskParams.deltaTime > exec.watchTime)
        {
            // draw the things
            DebugDraw.DrawLine(taskParams.position, naturalPositionAfterInterval, DebugDraw.Style.Thick.Color(Color.blue));
            DebugDraw.DrawLine(taskParams.position, desiredPosition, DebugDraw.Style.Thick.Color(Color.green));
            DebugDraw.DrawLine(naturalPositionAfterInterval, naturalPositionAfterInterval + accelerationPossibility, DebugDraw.Style.Thick.Color(Color.yellow));
        }

        //input.worldMovementDirection = accelerationPossibility.normalized * Mathf.Min(Vector3.Distance(naturalPositionAfterInterval, desiredPosition) / accelerationPossibility.magnitude, 1f);
        input.worldMovementDirection = Vector3.ClampMagnitude((desiredPosition - taskParams.position).normalized + accelerationPossibility.normalized * Mathf.Min(Vector3.Distance(naturalPositionAfterInterval, desiredPosition) / accelerationPossibility.magnitude, 1f), 1f);
    }

    public void OnDrawGizmos() { }
}

[System.Serializable]
public class TestBT_BrakeBeforePoint : IBotTask, ITestableBotTask
{
    private float time;

    private TestBotExecutor exec;

    private Vector3 target;
    private bool isBraking = false;

    public AnimationCurve desiredSpeedByDistanceToLine = new AnimationCurve();

    public void Init(in BotTaskParams taskParams) { time = 0f; isBraking = false; }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        Vector3 toTarget = (target - taskParams.position).Horizontal();
        // if friction is evaluated with * Mathf.Pow(friction, deltaTime * 35f)
        // velocity * friction^(deltaTime*35f) = targetSpeed
        // friction^(deltaTime*35f) = targetSpeed / velocity
        // Mathf.Log(friction^(deltaTime*35f)) = Mathf.Log(targetSpeed / velocity)
        // Mathf.Log(friction^(deltaTime*35f)) = Mathf.Log(targetSpeed / velocity)
        // deltaTime*35f*Mathf.Log(friction) = Mathf.Log(targetSpeed / velocity)
        // deltaTime = Mathf.Log(targetSpeed / velocity) / (35f * Mathf.Log(friction))

        float targetSpeed = 0.4f;
        float timeToSlowAndStop = Mathf.Log(targetSpeed / taskParams.movement.velocity.magnitude) / (35f * Mathf.Log(taskParams.movement.friction));

        // dist += velocity
        // velocity = velocity * friction
        // dist += velocity
        // velocity = velocity * friction

        // dist += velocity * friction + (velocity - friction) * friction...
        // dist += velocity*friction + velocity*friction - friction*friction
        // dist += velocity(friction + friction) - friction*friction
        Vector3 velocity = taskParams.movement.velocity;
        float friction = taskParams.movement.friction;
        float distanceMovedDuringBrakingTime = velocity.magnitude / (-35f * Mathf.Log(friction)) * (1f - Mathf.Pow(friction, 35f * timeToSlowAndStop));
        if (toTarget.magnitude < distanceMovedDuringBrakingTime)
            isBraking = true;

        toTarget.Normalize();
        input.worldMovementDirection = isBraking ? Vector3.zero : toTarget;
    }

    public void InitTests(TestBotExecutor exec)
    {
        this.exec = exec;
        if (exec.targetPositions.Count > 0)
        {
            target = exec.targetPositions[0];
        }
    }

    public void OnDrawGizmos()
    {
    }
}

[System.Serializable]
public class TestBT_RunToPointThenForceDirection : IBotTask, ITestableBotTask
{
    [Range(0, 360)]
    public float directionToForce = 0f;
    public float magnitude = 1f;

    private Vector3 preTurnDirection;

    private TestBotExecutor exec;

    public void Init(in BotTaskParams taskParams) { }

    public void InitTests(TestBotExecutor exec) => this.exec = exec;

    public void OnDrawGizmos() { }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
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
public class TestBT_RunToPointThenCircle : IBotTask, ITestableBotTask
{
    [Range(0, 360)]
    public float circleDegreesPerSecond = 0f;
    [Range(0f, 5f)]
    public float circleDuration = 1f;

    private Vector3 preCircleDirection;
    private float currentCircleRad = 0f;
    private float currentCircleTime = 0f;

    private TestBotExecutor exec;

    public void Init(in BotTaskParams taskParams) { }

    public void InitTests(TestBotExecutor exec) => this.exec = exec;

    public void OnDrawGizmos() { }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
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