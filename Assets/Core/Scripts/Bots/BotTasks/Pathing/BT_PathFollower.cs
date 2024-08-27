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
        this.canPathfollowerIgnore = false;
    }

    public Vector3 position;
    public float x { get => position.x; set => position.x = value; }
    public float y { get => position.y; set => position.y = value; }
    public float z { get => position.z; set => position.z = value; }

    public Color debugColor;
    public PathPointTask task;
    public bool canPathfollowerIgnore;

    public static implicit operator Vector3(PathPoint p) => p.position;
    public static implicit operator PathPoint(Vector3 v) => new PathPoint { position = v };
}

public class BT_PathFollower : ITestableBotTask, IBotTask, IBotDebugDraws
{
    public List<PathPoint> targets = new List<PathPoint>();
    [SerializeReference, PolymorphicTypeSelector]
    public List<PathFilter> pathFilters = new List<PathFilter>();
    public int currentTargetIndex = 0;
    public Vector3 startVelocity;

    private float characterHalfHeight = 0.5f;

    public float targetHorizontalRadius;
    private float targetVerticalRadius = 2f;

    private Vector3 previousPosition;
    private Vector3 previousVelocity;

    private Vector3 debugWatchPosition;
    private Vector3 debugWatchTargetPosition;

    public bool hasReachedEnd => currentTargetIndex == targets.Count;

    public void SetupPath(IReadOnlyCollection<PathPoint> pathPoints, Vector3 startVelocity, float targetRadius)
    {
        this.targets.Clear();
        this.targets.Capacity = pathPoints.Count;
        this.targets.AddRange(pathPoints);
        SetupPathForCurrentPathPoints(startVelocity, targetRadius);
    }

    public void SetupPath(IReadOnlyCollection<Vector3> targets, Vector3 startVelocity, float targetRadius)
    {
        this.targets.Clear();
        foreach (Vector3 target in targets)
            this.targets.Add(target);
        SetupPathForCurrentPathPoints(startVelocity, targetRadius);
    }

    private void SetupPathForCurrentPathPoints(Vector3 startVelocity, float targetRadius)
    {
        this.startVelocity = startVelocity;
        this.targetHorizontalRadius = targetRadius;
        this.currentTargetIndex = 0;

        foreach (PathFilter pathFilter in pathFilters)
        {
            if (!pathFilter.enabled)
                continue;
            pathFilter.Init();
            pathFilter.Apply(targets);
            if (pathFilter.hasError)
            {
                // Find the ring where the error happened
                // regenerate the path excluding that ring
            }
        }
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
            {
                do
                {
                    currentTargetIndex++;
                    // keep incrementing it if these are path points we can ignore
                } while (currentTargetIndex < targets.Count && targets[currentTargetIndex].canPathfollowerIgnore);
            }
        }

        if (taskParams.isWatchTime && currentTargetIndex < targets.Count)
        {
            debugWatchPosition = taskParams.position;
            debugWatchTargetPosition = targets[currentTargetIndex];
        }

        // Run path point tasks
        PathPointTaskParams pathPointTaskParams = new PathPointTaskParams()
        {
            botTaskParams = taskParams,
            pathPoints = targets,
            currentPathPoint = currentTargetIndex
        };

        foreach (PathFilter filter in pathFilters)
        {
            filter.Update(in pathPointTaskParams, ref input);
        }

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
        var lineStyle = DebugDraw.Style.DefaultWhite.Thickness(1f);
        for (int i = 0; i < targets.Count - 1; i++)
        {
            DebugDraw.DrawCross(targets[i], 1f, DebugDraw.Style.DefaultWhite.Color(targets[i].debugColor));
            DebugDraw.DrawLine(targets[i] + new Vector3(0f, 0.05f, 0f), targets[i + 1] + new Vector3(0f, 0.05f, 0f), lineStyle);
        }

        DebugDraw.DrawLine(debugWatchPosition, debugWatchTargetPosition, Color.blue);
    }
}
