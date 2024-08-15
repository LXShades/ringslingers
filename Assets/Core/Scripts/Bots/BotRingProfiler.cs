using System.Collections.Generic;
using UnityEngine;
public struct RingLine
{
    public Vector3 start;
    public Vector3 end;
    public float length;
    public int numRings;
}

public class BotRingProfiler : MonoBehaviour
{
    public static BotRingProfiler singleton;

    public IReadOnlyList<RingLine> ringLines => internalRingLines;

    private List<RingLine> internalRingLines = new List<RingLine>();

    private void Start()
    {
        singleton = this;

        GenerateRingProfile(internalRingLines);
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

                foreach (Ring otherRing in rings)
                {
                    if (nearbyCandidates.Contains(otherRing))
                        continue;

                    Vector3 otherRingPosition = otherRing.transform.position;
                    if (Vector3.Distance(otherRingPosition, forwardRingPosition) <= closestForwardRingDist)
                    {
                        float straightness = forwardRing != baseRing ? Vector3.Dot((otherRingPosition - forwardRingPosition).normalized, direction) : 1f;
                        if (straightness >= 1f - straightnessThreshold)
                        {
                            if (forwardRing == baseRing)
                                direction = (otherRingPosition - forwardRingPosition).normalized;
                            closestForwardRingDist = Vector3.Distance(otherRingPosition, forwardRingPosition);
                            nextForwardRing = otherRing;
                        }
                    }
                    else if (forwardRing != baseRing && Vector3.Distance(otherRingPosition, backwardRingPosition) <= closestBackwardRingDist)
                    {
                        float straightness = Vector3.Dot((otherRingPosition - backwardRingPosition).normalized, direction);
                        if (straightness <= -1f + straightnessThreshold)
                        {
                            closestBackwardRingDist = Vector3.Distance(otherRingPosition, backwardRingPosition);
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
                    numRings = nearbyCandidates.Count + 1 // include base ring
                };

                foreach (var nearbyRing in nearbyCandidates)
                    rings.Remove(nearbyRing);

                outLines.Add(newRingLine);
            }
        }
    }

    /// <summary>
    /// used by editor tools
    /// </summary>
    public static void ForceInit()
    {
        singleton = FindObjectOfType<BotRingProfiler>();
        singleton?.Start();
    }
}
