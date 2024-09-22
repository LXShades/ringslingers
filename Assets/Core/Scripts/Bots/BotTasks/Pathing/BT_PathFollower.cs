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

[System.Serializable]
public struct PathPointAcceptanceRange
{
    public float maxYOffset;
    public float minYOffset;
    public float horizontalRadius;
}

public struct PathPoint
{
    public PathPoint(Vector3 position)
    {
        this.position = position;
        this.debugColor = Color.white;
        this.task = null;
        this.canPathfollowerIgnore = false;
        this.canPathfollowerSkip = true;
        this.acceptanceRange = new PathPointAcceptanceRange();
    }

    public Vector3 position;
    public float x { get => position.x; set => position.x = value; }
    public float y { get => position.y; set => position.y = value; }
    public float z { get => position.z; set => position.z = value; }

    // Path radii. If zero, uses path follower defaults. If our position enters area it is considered 'reached'.
    public PathPointAcceptanceRange acceptanceRange;

    public Color debugColor;
    public PathPointTask task;
    public bool canPathfollowerIgnore;
    public bool canPathfollowerSkip;

    public static implicit operator Vector3(PathPoint p) => p.position;
    public static implicit operator PathPoint(Vector3 v) => new PathPoint(v);
}

public class BT_PathFollower : ITestableBotTask, IBotTask, IBotDebugDraws
{
    public List<PathPoint> targets = new List<PathPoint>();
    [SerializeReference, PolymorphicTypeSelector]
    public List<PathFilter> pathFilters = new List<PathFilter>();
    public int currentTargetIndex = 0;
    public Vector3 startVelocity;

    private float characterHalfHeight = 0.5f;

    public PathPointAcceptanceRange defaultPathPointAcceptanceRange = new PathPointAcceptanceRange() { minYOffset = -0.5f, maxYOffset = 1f, horizontalRadius = 0.25f };

    private Vector3 previousPosition;
    private Vector3 previousVelocity;

    private Vector3 debugWatchPosition;
    private Vector3 debugWatchTargetPosition;

    public bool hasReachedEnd => currentTargetIndex == targets.Count;

    public void SetupPath(IReadOnlyCollection<PathPoint> pathPoints, Vector3 startVelocity)
    {
        this.targets.Clear();
        this.targets.Capacity = pathPoints.Count;
        this.targets.AddRange(pathPoints);
        SetupPathForCurrentPathPoints(startVelocity);
    }

    public void SetupPath(IReadOnlyCollection<Vector3> targets, Vector3 startVelocity)
    {
        this.targets.Clear();
        foreach (Vector3 target in targets)
            this.targets.Add(target);
        SetupPathForCurrentPathPoints(startVelocity);
    }

    private void SetupPathForCurrentPathPoints(Vector3 startVelocity)
    {
        this.startVelocity = startVelocity;
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

    public bool IsInAcceptanceRange(Vector3 position, in PathPoint pathPoint)
    {
        PathPointAcceptanceRange acceptanceRange = GetAcceptanceRangeForPoint(pathPoint);
        return VectorExtensions.HorizontalDistance(position, pathPoint.position) < acceptanceRange.horizontalRadius && position.y >= pathPoint.position.y + acceptanceRange.minYOffset && position.y <= pathPoint.position.y + acceptanceRange.maxYOffset;
    }

    public PathPointAcceptanceRange GetAcceptanceRangeForPoint(in PathPoint pathPoint)
    {
        return new PathPointAcceptanceRange()
        {
            horizontalRadius = pathPoint.acceptanceRange.horizontalRadius != 0f ? pathPoint.acceptanceRange.horizontalRadius : defaultPathPointAcceptanceRange.horizontalRadius,
            minYOffset = pathPoint.acceptanceRange.minYOffset != 0f ? pathPoint.acceptanceRange.minYOffset : defaultPathPointAcceptanceRange.minYOffset,
            maxYOffset = pathPoint.acceptanceRange.maxYOffset != 0f ? pathPoint.acceptanceRange.maxYOffset : defaultPathPointAcceptanceRange.maxYOffset,
        };
    }

    public virtual void InitTests(TestBotExecutor exec) => SetupPath(exec.targetPositions, exec.startVelocity);

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

        if (IsInAcceptanceRange(positionClosestToTarget, targetPosition))
        {
            return true;
        }

        return false;
    }

    public virtual void OnPreFilterMove(in BotTaskParams taskParams, ref CharacterInput input) { }

    public virtual void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        // Check if we crossed the latest point
        if (currentTargetIndex < targets.Count && previousVelocity.sqrMagnitude > 0f)
        {
            // We can check all the following points (skip points!) until we hit a point that cannot be skipped
            for (int nextValidTargetIndex = currentTargetIndex; nextValidTargetIndex < targets.Count; nextValidTargetIndex++)
            {
                if (HasHitTarget(previousPosition, taskParams.position, targets[nextValidTargetIndex]))
                {
                    currentTargetIndex = nextValidTargetIndex + 1;
                    while (currentTargetIndex < targets.Count && targets[currentTargetIndex].canPathfollowerIgnore)
                    {
                        // keep incrementing it if these are path points we can ignore
                        currentTargetIndex++;
                    }
                }

                if (!targets[nextValidTargetIndex].canPathfollowerSkip)
                    break;
            }
        }

        if (taskParams.isWatchTime && currentTargetIndex < targets.Count)
        {
            debugWatchPosition = taskParams.position;
            debugWatchTargetPosition = targets[currentTargetIndex];
        }

        OnPreFilterMove(in taskParams, ref input);

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
        for (int i = 0; i < targets.Count; i++)
        {
            PathPointAcceptanceRange acceptanceRange = GetAcceptanceRangeForPoint(targets[i]);
            DebugDraw.Style targetStyle = DebugDraw.Style.DefaultWhite.Color(targets[i].debugColor);

            DebugDraw.DrawCross(targets[i], 1f, targetStyle);
            DebugDraw.DrawCapsule(targets[i] + new Vector3(0f, acceptanceRange.minYOffset, 0f), targets[i] + new Vector3(0f, acceptanceRange.maxYOffset, 0f), acceptanceRange.horizontalRadius, targetStyle);

            if (i < targets.Count - 1)
                DebugDraw.DrawLine(targets[i] + new Vector3(0f, 0.05f, 0f), targets[i + 1] + new Vector3(0f, 0.05f, 0f), lineStyle);
        }

        DebugDraw.DrawLine(debugWatchPosition, debugWatchTargetPosition, Color.blue);
    }
}
