using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PathFilter_Thoks : PathFilter
{
    public override void Apply(List<PathPoint> points)
    {
        base.Apply(points);

        BotNavMeshBuilder.EnsureInit();

        for (int idx = 2; idx < points.Count; idx++)
        {
            // Estimate whether the current point is on the ground
            bool hasHit = NavMesh.SamplePosition(points[idx].position, out NavMeshHit pointHit, 2f, ~0);

            if (hasHit)
            {
                // if so, put thok point here, yolo
                PathPoint newPoint = new PathPoint(pointHit.position);
                newPoint.debugColor = Color.red;
                newPoint.task = new PathPointTask_TryThok() { pathPointIndex = idx };
                points.Insert(idx, newPoint);
                idx++;
            }
        }
    }
}

public class PathPointTask_TryThok : PathPointTask
{
    float timeTilReleaseJump = 0f;
    bool shouldThokNextUpdate = false;

    public override void Update(in PathPointTaskParams taskParams, ref CharacterInput input)
    {
        base.Update(taskParams, ref input);

        if (Vector3.Distance(taskParams.botTaskParams.position, taskParams.pathPoints[pathPointIndex]) < 0.5f && taskParams.botTaskParams.movement.isOnGround)
        {
            timeTilReleaseJump = 0.15f;
        }

        input.btnJump |= timeTilReleaseJump > 0f;

        if (shouldThokNextUpdate)
        {
            if (input.btnJump == true)
                input.btnJump = false;
            else
            {
                input.aimDirection = (taskParams.pathPoints[taskParams.currentPathPoint] - taskParams.botTaskParams.position).normalized; // we need to look towards the direction we're thokking
                input.btnJump = true;
                shouldThokNextUpdate = false;
            }
        }

        if (timeTilReleaseJump > 0f)
        {
            timeTilReleaseJump -= taskParams.botTaskParams.deltaTime;
            if (timeTilReleaseJump < 0f)
            {
                shouldThokNextUpdate = true;
                input.btnJump = false; // release the thokken
            }
        }
    }
}