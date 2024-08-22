using System.Collections.Generic;
using UnityEngine;

public struct PathPointTaskParams
{
    public BotTaskParams botTaskParams;
    public List<PathPoint> pathPoints;
    public int currentPathPoint;
}

public class PathPointTask
{
    public int pathPointIndex;

    public virtual void Update(in PathPointTaskParams taskParams, ref CharacterInput input) { }
}

public struct PathPoint
{
    public PathPoint(Vector3 position)
    {
        this.position = position;
        this.debugColor = Color.white;
        this.task = null;
    }

    public Vector3 position;
    public float x { get => position.x; set => position.x = value; }
    public float y { get => position.y; set => position.y = value; }
    public float z { get => position.z; set => position.z = value; }

    public Color debugColor;
    public PathPointTask task;

    public static implicit operator Vector3(PathPoint p) => p.position;
    public static implicit operator PathPoint(Vector3 v) => new PathPoint { position = v };
}

public class BT_PathFollower : ITestableBotTask, IBotTask, IBotDebugDraws
{
    public List<PathPoint> targets = new List<PathPoint>();
    public int currentTargetIndex = 0;
    public Vector3 startVelocity;

    private float characterHalfHeight = 0.5f;

    public float targetHorizontalRadius;
    private float targetVerticalRadius = 2f;

    private Vector3 previousPosition;
    private Vector3 previousVelocity;

    public bool hasReachedEnd => currentTargetIndex == targets.Count;

    public void SetupPath(IReadOnlyCollection<PathPoint> pathPoints, Vector3 startVelocity, float targetRadius)
    {
        this.targets.Clear();
        this.targets.Capacity = pathPoints.Count;
        this.targets.AddRange(pathPoints);
        this.startVelocity = startVelocity;
        this.targetHorizontalRadius = targetRadius;
        this.currentTargetIndex = 0;
    }

    public void SetupPath(IReadOnlyCollection<Vector3> targets, Vector3 startVelocity, float targetRadius)
    {
        this.targets.Clear();
        foreach (Vector3 target in targets)
            this.targets.Add(target);
        this.startVelocity = startVelocity;
        this.targetHorizontalRadius = targetRadius;
        this.currentTargetIndex = 0;
    }

    public virtual void InitTests(TestBotExecutor exec) => SetupPath(exec.targetPositions, exec.startVelocity, exec.targetRadius);

    public virtual void Init(in BotTaskParams taskParams)
    {
        previousPosition = taskParams.position;
        previousVelocity = taskParams.movement.velocity;
    }

    public bool HasHitTarget(Vector3 previousPosition, Vector3 nextPosition, Vector3 targetPosition)
    {
        Vector3 positionClosestToTarget = nextPosition;

        if (nextPosition != previousPosition)
        {
            Vector3 trajectoryNormalized = (nextPosition - previousPosition).normalized;
            float targetDot = Vector3.Dot(trajectoryNormalized, targetPosition);
            float lastDot = Vector3.Dot(trajectoryNormalized, previousPosition);
            float nextDot = Vector3.Dot(trajectoryNormalized, nextPosition);

            if (lastDot < targetDot && nextDot >= targetDot)
            {
                // we zoomed past the point, see if we passed closely enough
                positionClosestToTarget = Vector3.Lerp(previousPosition, nextPosition, (targetDot - lastDot) / (nextDot - lastDot));
            }
        }

        float horDist = VectorExtensions.HorizontalDistance(positionClosestToTarget, targets[currentTargetIndex]);
        float vertDist = Mathf.Abs(positionClosestToTarget.y + characterHalfHeight - targets[currentTargetIndex].y);
        if (horDist < targetHorizontalRadius && vertDist <= targetVerticalRadius)
        {
            return true;
        }

        return false;
    }

    public virtual void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        // Check if we crossed the latest point
        if (currentTargetIndex < targets.Count && previousVelocity.sqrMagnitude > 0f)
        {
            if (HasHitTarget(previousPosition, taskParams.position, targets[currentTargetIndex]))
                currentTargetIndex++;
        }

        // Run path point tasks
        PathPointTaskParams pathPointTaskParams = new PathPointTaskParams()
        {
            botTaskParams = taskParams,
            pathPoints = targets,
            currentPathPoint = currentTargetIndex
        };
        foreach (PathPoint point in targets)
        {
            if (point.task != null)
                point.task.Update(in pathPointTaskParams, ref input);
        }

        previousPosition = taskParams.position;
        previousVelocity = taskParams.movement.velocity;
    }

    public virtual void DrawDebugs()
    {
        for (int i = 0; i < targets.Count - 1; i++)
            DebugDraw.DrawCross(targets[i], 1f, DebugDraw.Style.DefaultWhite.Color(targets[i].debugColor));
    }
}
