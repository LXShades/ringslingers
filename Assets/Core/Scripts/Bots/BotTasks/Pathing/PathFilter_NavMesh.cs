using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PathFilter_NavMesh : PathFilter
{
    public Color pointDebugColor = Color.green;

    public override void Apply(List<PathPoint> points)
    {
        base.Apply(points);

        BotNavMeshBuilder.EnsureInit();

        NavMeshPath path = new NavMeshPath();
        Vector3 verticalPadding = new Vector3(0f, 0.3f, 0f);
        for (int idx = 0; idx < points.Count - 1; idx++)
        {
            bool hasSourcePosition = NavMesh.SamplePosition(points[idx] + verticalPadding, out NavMeshHit sourceHit, 20.0f, ~0);
            bool hasTargetPosition = NavMesh.SamplePosition(points[idx + 1] + verticalPadding, out NavMeshHit targetHit, 20.0f, ~0);

            if (hasTargetPosition && hasSourcePosition)
            {
                path.ClearCorners();
                if (!NavMesh.CalculatePath(sourceHit.position, targetHit.position, ~0, path) || path.status == NavMeshPathStatus.PathPartial)
                {
                    OnError(idx + 1, $"No path valid between [{idx}] ({sourceHit.position}) and [{idx + 1}] ({targetHit.position})");
                }

                // Insert the new corners into the path, if there are new corners
                Vector3[] corners = path.corners; // unity allocs each time we retrieve this???
                if (corners.Length > 2)
                {
                    for (int generatedIdx = 1; generatedIdx < corners.Length - 1; generatedIdx++)
                    {
                        points.Insert(idx + generatedIdx, new PathPoint(corners[generatedIdx]) { debugColor = pointDebugColor });
                    }
                    idx += corners.Length - 3;
                }
            }
            else
            {
                // do an error somehow
                // idea: wouldn't it be cool if you could click vectors in logs and see them on screen??
                OnError(hasSourcePosition ? idx + 1 : idx, $"Navmesh not valid for both points: [{idx}] {points[idx]}={hasSourcePosition} and [{idx+1}] {points[idx + 1]}={hasTargetPosition}");
                DebugDraw.DrawSphere(points[idx + 1], 5f, Color.red);
            }
        }
    }
}