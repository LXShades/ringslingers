using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;

public struct RingLink
{
    public int target;
    public float distance;
}

public struct RingLine
{
    public const int maxNumLinks = 16;

    public List<RespawnableItem> rings;
    public Vector3 start;
    public Vector3 end;
    public float length;
    public int numRings;
    public RingLink[] links;
}

public class BotRingProfiler : MonoBehaviour
{
    private struct TempLink
    {
        public int a;
        public int b;
        public bool canGoAtoB;
        public bool canGoBtoA;
        public float distance;
    }

    public static BotRingProfiler singleton;

    public IReadOnlyList<RingLine> ringLines => internalRingLines;

    private List<RingLine> internalRingLines = new List<RingLine>();

    private void Start()
    {
        if (singleton != this)
        {
            singleton = this;

            GenerateRingProfile(internalRingLines);
            AssignRingLinks(internalRingLines);
        }
    }

    public static void GenerateRingProfile(List<RingLine> outLines)
    {
        outLines.Clear();
        HashSet<Ring> rings = new HashSet<Ring>();

        foreach (Ring ring in GameObject.FindObjectsByType<Ring>(FindObjectsSortMode.None))
            rings.Add(ring);

        HashSet<Ring> nearbyCandidates = new HashSet<Ring>();
        float distanceThreshold = 2f;
        float straightnessThreshold = 0.05f;
        while (rings.Count > 0)
        {
            nearbyCandidates.Clear();

            Ring baseRing = null;
            foreach (Ring ring in rings)
            {
                baseRing = ring;
                break;
            }
            rings.Remove(baseRing);

            Vector3 direction = Vector3.zero;
            Ring forwardRing = baseRing, backwardRing = baseRing;
            Ring nextForwardRing, nextBackwardRing;

            do
            {
                nextForwardRing = nextBackwardRing = null;

                Vector3 forwardRingPosition = forwardRing.transform.position;
                Vector3 backwardRingPosition = backwardRing.transform.position;
                float closestForwardRingDist = distanceThreshold;
                float closestBackwardRingDist = distanceThreshold;

                /*
                 * for debugging the curve in Meadow Match
                if (forwardRing.transform.parent.name == "RingArc")
                {
                    int i = 0;
                    Debug.Log("da ring");
                }*/

                foreach (Ring otherRing in rings)
                {
                    if (nearbyCandidates.Contains(otherRing))
                        continue;

                    Vector3 otherRingPosition = otherRing.transform.position;
                    float distFromForward = Vector3.Distance(otherRingPosition, forwardRingPosition);
                    float distFromBackward = Vector3.Distance(otherRingPosition, backwardRingPosition);
                    if (distFromForward <= closestForwardRingDist)
                    {
                        float straightness = forwardRing != baseRing ? Vector3.Dot((otherRingPosition - forwardRingPosition).normalized, direction) : 1f;
                        if (straightness >= 1f - straightnessThreshold)
                        {
                            if (forwardRing == baseRing)
                                direction = (otherRingPosition - forwardRingPosition).normalized;
                            closestForwardRingDist = distFromForward;
                            nextForwardRing = otherRing;
                        }
                    }
                    else if (forwardRing != baseRing && distFromBackward <= closestBackwardRingDist)
                    {
                        float straightness = Vector3.Dot((otherRingPosition - backwardRingPosition).normalized, direction);
                        if (straightness <= -1f + straightnessThreshold)
                        {
                            closestBackwardRingDist = distFromBackward;
                            nextBackwardRing = otherRing;
                        }
                    }
                }

                if (nextForwardRing)
                {
                    forwardRing = nextForwardRing;
                    nearbyCandidates.Add(nextForwardRing);
                }
                if (nextBackwardRing)
                {
                    backwardRing = nextBackwardRing;
                    nearbyCandidates.Add(nextBackwardRing);
                }
            } while (nextForwardRing != null || nextBackwardRing != null);

            if (nearbyCandidates.Count > 0 && forwardRing && backwardRing)
            {
                RingLine newRingLine = new RingLine()
                {
                    start = forwardRing.transform.position,
                    end = backwardRing.transform.position,
                    length = Vector3.Distance(forwardRing.transform.position, backwardRing.transform.position),
                    rings = new List<RespawnableItem>(nearbyCandidates.Select(x => x.respawnableItem)),
                    numRings = nearbyCandidates.Count + 1 // include base ring
                };

                foreach (var nearbyRing in nearbyCandidates)
                    rings.Remove(nearbyRing);

                outLines.Add(newRingLine);
            }
        }
    }

    public static void AssignRingLinks(List<RingLine> ringLines)
    {
        BotNavMeshBuilder.EnsureInit();

        // Find all links between all ring lines
        List<TempLink> linkCandidates = new List<TempLink>();
        NavMeshPath path = new NavMeshPath();
        for (int idxA = 0; idxA < ringLines.Count; idxA++)
        {
            Vector3 lineAPosition = (ringLines[idxA].start + ringLines[idxA].end) * 0.5f;

            for (int idxB = idxA + 1; idxB < ringLines.Count; idxB++)
            {
                Vector3 lineBPosition = (ringLines[idxB].start + ringLines[idxB].end) * 0.5f;

                bool hasSourcePosition = NavMesh.SamplePosition(lineAPosition, out NavMeshHit hitA, 2.0f, ~0);
                bool hasTargetPosition = NavMesh.SamplePosition(lineBPosition, out NavMeshHit hitB, 2.0f, ~0);

                if (!hasSourcePosition || !hasTargetPosition)
                    continue;

                TempLink newLink = new TempLink();
                path.ClearCorners();
                if (NavMesh.CalculatePath(hitA.position, hitB.position, ~0, path) && path.status == NavMeshPathStatus.PathComplete)
                    newLink.canGoAtoB = true;
                if (NavMesh.CalculatePath(hitB.position, hitA.position, ~0, path) && path.status == NavMeshPathStatus.PathComplete)
                    newLink.canGoBtoA = true;
                if (newLink.canGoBtoA || newLink.canGoBtoA)
                {
                    // todo: distance should reflect path length
                    newLink.a = idxA;
                    newLink.b = idxB;
                    newLink.distance = Vector3.Distance(hitA.position, hitB.position);
                    linkCandidates.Add(newLink);
                }
            }
        }

        // Sort the links
        linkCandidates.Sort((a, b) => a.distance < b.distance ? -1 : 1);

        // Hand the links out to the ring lines
        List<RingLink> ringLinks = new List<RingLink>();
        for (int idx = 0; idx < ringLines.Count; idx++)
        {
            RingLine ringLine = ringLines[idx];

            ringLinks.Clear();

            foreach (TempLink link in linkCandidates)
            {
                if ((link.a == idx && link.canGoAtoB) || (link.b == idx && link.canGoBtoA))
                {
                    ringLinks.Add(new RingLink() { distance = link.distance, target = link.a == idx ? link.b : link.a });

                    if (ringLinks.Count >= RingLine.maxNumLinks)
                        break;
                }
            }

            ringLine.links = ringLinks.ToArray();
            ringLines[idx] = ringLine;
        }
    }

    /// <summary>
    /// used by editor tools
    /// </summary>
    public static void EnsureInit()
    {
        if (singleton == null)
        {
            FindObjectOfType<BotRingProfiler>().Start();
        }
    }
}
