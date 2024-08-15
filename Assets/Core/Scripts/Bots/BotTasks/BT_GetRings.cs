using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BT_GetRings : ITestableBotTask, IBotTask
{
    [SerializeReference]
    public BT_PrecalculatedPathFollower pathFollower = new BT_CircleBasedPathFollow();

    private List<Vector3> ringPath = new List<Vector3>();

    private Vector3 startVelocity;

    public bool drawRingPath = true;

    public void InitTests(TestBotExecutor exec)
    {
        startVelocity = exec.startVelocity;
        BotRingProfiler.ForceInit();
        pathFollower.InitTests(exec);
    }

    public void Init(in BotTaskParams taskParams)
    {
        GeneratePathBetweenRings(taskParams.characterObject.transform.position, BotRingProfiler.singleton.ringLines, ringPath);

        (pathFollower as BT_CircleBasedPathFollow).SetupPath(ringPath, startVelocity);
        pathFollower.Init(taskParams);
    }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        pathFollower.Update(in taskParams, ref input);
    }

    public static void GeneratePathBetweenRings(Vector3 startingPosition, IReadOnlyList<RingLine> ringLines, List<Vector3> outPath)
    {
        outPath.Clear();

        // Maybe collect rings in a miscellaneous circle
        // Travel clockwise or anticlockwise or whatevs
        float verticalThreshold = 0.75f;
        HashSet<int> visitedRingLines = new HashSet<int>();
        int nextRingLine;

        Vector3 currentExitPosition = startingPosition;
        Vector3 centrePosition = Vector3.zero;

        foreach (RingLine ring in ringLines)
            centrePosition += ring.start + ring.end;
        centrePosition /= ringLines.Count * 2;
        DebugDraw.DrawSphere(centrePosition, 1f, DebugDraw.Style.Thick);

        outPath.Add(startingPosition);

        float startingAng = Mathf.Atan2(startingPosition.z - centrePosition.z, startingPosition.x - centrePosition.x);
        float lastAng = startingAng;
        do
        {
            nextRingLine = -1;

            float closestDist = float.MaxValue;
            float closestAng = 0f;
            Vector3 entryPosition = default, exitPosition = default;
            for (int i = 0; i < ringLines.Count; i++)
            {
                if (visitedRingLines.Contains(i))
                    continue;
                if (Mathf.Abs(ringLines[i].start.y - startingPosition.y) > verticalThreshold || Mathf.Abs(ringLines[i].end.y - startingPosition.y) > verticalThreshold)
                    continue;

                float startDist = VectorExtensions.HorizontalDistance(ringLines[i].start, currentExitPosition);
                float endDist = VectorExtensions.HorizontalDistance(ringLines[i].end, currentExitPosition);
                float ang = Mathf.Atan2(ringLines[i].start.z - centrePosition.z, ringLines[i].start.x - centrePosition.x);
                float deltaAng = Mathf.DeltaAngle(lastAng * Mathf.Rad2Deg, ang * Mathf.Rad2Deg);

                if (Mathf.Min(startDist, endDist) < closestDist && deltaAng > 0f)
                {
                    nextRingLine = i;
                    closestDist = startDist < endDist ? startDist : endDist;
                    closestAng = ang;
                    entryPosition = startDist < endDist ? ringLines[i].start : ringLines[i].end;
                    exitPosition = startDist < endDist ? ringLines[i].end : ringLines[i].start;
                }
            }

            if (nextRingLine != -1)
            {
                visitedRingLines.Add(nextRingLine);
                outPath.Add(entryPosition);
                outPath.Add(exitPosition);
                currentExitPosition = exitPosition;
                lastAng = closestAng;
            }
        } while (nextRingLine != -1);
    }

    public void OnDrawGizmos()
    {
        pathFollower.OnDrawGizmos();

        if (drawRingPath)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < ringPath.Count - 1; i++)
                Gizmos.DrawLine(ringPath[i] + new Vector3(0f, 0.05f, 0f), ringPath[i + 1] + new Vector3(0f, 0.05f, 0f));


            if (BotRingProfiler.singleton)
            {
                foreach (var ringLine in BotRingProfiler.singleton.ringLines)
                    Gizmos.DrawLine(ringLine.start + new Vector3(0f, 0.05f, 0f), ringLine.end + new Vector3(0f, 0.05f, 0f));
            }
        }
    }
}
