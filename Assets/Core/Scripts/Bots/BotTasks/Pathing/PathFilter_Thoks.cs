using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PathFilter_Thoks : PathFilter
{
    public override void Apply(List<PathPoint> points)
    {
        base.Apply(points);

        BotNavMeshBuilder.EnsureInit();

        for (int idx = 0; idx < points.Count; idx++)
        {
            // Estimate whether the current point is on the ground
            NavMesh.SamplePosition(points[idx].position, out NavMeshHit pointHit, 2f, ~0);

            // if so, 
        }
    }
}
