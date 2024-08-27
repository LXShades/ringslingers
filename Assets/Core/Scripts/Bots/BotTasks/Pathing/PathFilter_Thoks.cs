using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PathFilter_Thoks : PathFilter
{
    public float beelineLengthThreshold = 8f;
    public float jumpThokDelaySeconds = 0.1f;
    public Color pointDebugColor = Color.red;
    public float velocityPredictionWeight = 1f;

    public override void Apply(List<PathPoint> points)
    {
        base.Apply(points);

        shouldThokNextUpdate = false;
        timeTilReleaseJump = 0f;

        BotNavMeshBuilder.EnsureInit();

        for (int idx = 0; idx < points.Count - 1; idx++)
        {
            // Estimate whether the current point is on the ground
            float prevToNext = VectorExtensions.HorizontalDistance(points[idx], points[idx + 1]);
            bool hasHit = NavMesh.SamplePosition(points[idx].position, out NavMeshHit pointHit, 2f, ~0);

            // todo: fix this. currently starting at 2f so that we can predict / jump -before- the thok point without it going 'but no your current target isn't ahead of you yet' and it gets messy
            for (float distanceCovered = 2f; distanceCovered < prevToNext; distanceCovered += 1f)
            {
                Vector3 currentPosition = pointHit.position + (points[idx + 1].position - points[idx].position).normalized * distanceCovered;

                // if we can thok along this line, add a TryThok point
                if (hasHit && prevToNext - distanceCovered >= beelineLengthThreshold)
                {
                    // if so, put thok point here
                    PathPoint newPoint = new PathPoint(currentPosition)
                    {
                        debugColor = pointDebugColor,
                        canPathfollowerIgnore = true,
                        task = new PathPointTask_TryThok() { pathPointIndex = idx + 1, jumpThokDelaySeconds = jumpThokDelaySeconds, targetPointPosition = points[idx + 1].position }
                    };
                    points.Insert(idx + 1, newPoint);
                    idx++;

                    distanceCovered += beelineLengthThreshold;
                }
            }
        }
    }

    private bool shouldThokNextUpdate = false;
    private float timeTilReleaseJump = 0f;

    public override void Update(in PathPointTaskParams taskParams, ref CharacterInput input)
    {
        base.Update(taskParams, ref input);

        if (timeTilReleaseJump <= 0f)
        {
            // Go through all points, finding thok points we've stepped on
            // todo not all points - some of them we shouldn't bother because we're well past htem
            for (int pointIdx = 0; pointIdx < taskParams.pathPoints.Count; pointIdx++)
            {
                if (taskParams.pathPoints[pointIdx].task is PathPointTask_TryThok thokPoint)
                {
                    // todo: jump / prepare thok if we expect we're _about_ to hit the thok point
                    Vector3 positionOrPredictedPosition = Vector3.Lerp(taskParams.botTaskParams.position, taskParams.botTaskParams.position + taskParams.botTaskParams.movement.velocity * jumpThokDelaySeconds, velocityPredictionWeight);

                    // When reaching a thok point, try and thok towards the next point
                    // We want to make sure we don't thok past the point we're aiming for, though
                    if (taskParams.currentPathPoint >= pointIdx && taskParams.botTaskParams.movement.isOnGround && Vector3.Distance(positionOrPredictedPosition, taskParams.pathPoints[pointIdx]) < 0.5f)
                    {
                        // INITIATE THE JUMP HERE
                        timeTilReleaseJump = thokPoint.jumpThokDelaySeconds;
                    }

                }
            }
        }

        // Hold jump until release
        input.btnJump |= timeTilReleaseJump > 0f;

        // If releasing now, thok with another jump press, and ensure thok is ready and pointing right direction
        if (shouldThokNextUpdate)
        {
            if (input.btnJump == true)
                input.btnJump = false;
            else
            {
                // todo maybe check the point(s) are still correct
                input.aimDirection = VectorExtensions.HorizontalNormalized(taskParams.pathPoints[taskParams.currentPathPoint] - taskParams.botTaskParams.position); // we need to look towards the direction we're thokking, and it needs to be ahead of us
                input.btnJump = true;
                shouldThokNextUpdate = false;
            }
        }

        // Release next frame after the release timer hits zero
        if (timeTilReleaseJump > 0f)
        {
            timeTilReleaseJump -= taskParams.botTaskParams.deltaTime;
            if (timeTilReleaseJump <= 0f)
            {
                shouldThokNextUpdate = true;
                input.btnJump = false; // release the thokken
            }
        }
    }
}

public class PathPointTask_TryThok : PathPointTask
{
    float timeTilReleaseJump = 0f;
    public float jumpThokDelaySeconds = 0.1f;
    public Vector3 targetPointPosition; // would rather store the index of the next point, but path filters will mess with that
}