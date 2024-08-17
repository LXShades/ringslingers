using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BT_GetRings : ITestableBotTask, IBotTask, IBotDebugDraws
{
    [SerializeReference, PolymorphicTypeSelector]
    public List<PathFilter> pathFilters = new List<PathFilter>();
    [SerializeReference, PolymorphicTypeSelector]
    public BT_PathFollower pathFollower = new BT_TargetVelocityBasedPathFollower();

    private List<PathPoint> ringPath = new List<PathPoint>();

    public float targetPathLength = 5;
    public bool drawRingPath = true;

    public void InitTests(TestBotExecutor exec)
    {
        BotRingProfiler.ForceInit();
        pathFollower.InitTests(exec);
    }

    public void Init(in BotTaskParams taskParams)
    {
        GeneratePathBetweenRings(taskParams.characterObject.transform.position, BotRingProfiler.singleton.ringLines, ringPath, targetPathLength);

        foreach (PathFilter pathFilter in pathFilters)
        {

        }

        pathFollower.SetupPath(ringPath, taskParams.movement.velocity, 0.25f);
        pathFollower.Init(taskParams);
    }

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        pathFollower.Update(in taskParams, ref input);
    }

    public static void GeneratePathBetweenRings(Vector3 startingPosition, IReadOnlyList<RingLine> ringLines, List<PathPoint> outPath, float targetPathLength)
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
        float pathLength = 0f;

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
                if (Application.isPlaying)
                {
                    int numLivingRings = 0;
                    foreach (var ring in ringLines[i].rings)
                        numLivingRings += ring.isSpawned ? 1 : 0;
                    if (numLivingRings == 0)
                        continue;
                }

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
                pathLength += Vector3.Distance(currentExitPosition, exitPosition);

                currentExitPosition = exitPosition;
                lastAng = closestAng;

            }
        } while (nextRingLine != -1 && pathLength <= targetPathLength);
    }

    public void DrawDebugs()
    {
        pathFollower.DrawDebugs();

        if (drawRingPath)
        {
            DebugDraw.Style style = Color.yellow;
            for (int i = 0; i < ringPath.Count - 1; i++)
                DebugDraw.DrawLine(ringPath[i] + new Vector3(0f, 0.05f, 0f), ringPath[i + 1] + new Vector3(0f, 0.05f, 0f), style);

            if (BotRingProfiler.singleton)
            {
                foreach (var ringLine in BotRingProfiler.singleton.ringLines)
                    DebugDraw.DrawLine(ringLine.start + new Vector3(0f, 0.05f, 0f), ringLine.end + new Vector3(0f, 0.05f, 0f), style);
            }
        }
    }
}
