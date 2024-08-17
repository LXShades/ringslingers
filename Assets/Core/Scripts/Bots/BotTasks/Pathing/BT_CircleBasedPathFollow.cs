using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class BT_CircleBasedPathFollow : BT_PrecalculatedPathFollower, IBotTask
{
    public struct Circle
    {
        public Vector3 position;
        public float radius;
        public bool clockwise;
    }

    public struct Line
    {
        public Vector3 pointA;
        public Vector3 pointB;
    }

    private List<Circle> circles = new List<Circle>();
    private List<Line> lines = new List<Line>();
    public float turnCircleRadius = 3f;
    public float steerStrengthDegrees = 50f;
    [Range(0f, 1f)]
    public List<float> scaleByCircle = new List<float>();
    [Range(0f, 2f)]
    public float pushAwayForce = 0f;

    public override void Init(in BotTaskParams taskParams)
    {
        targetLineIdx = 0;
        isOnLine = false;
        circles.Clear();
        lines.Clear();

        InitTechnique_CirclesFirstThenDirections(in taskParams);
        base.Init(in taskParams);
    }

    public void InitTechnique_CirclesFirstThenDirections(in BotTaskParams taskParams)
    {
        // First circle will be our starting position and we'll line it up with our velocity
        Vector3 startPosition = taskParams.characterObject.transform.position;
        TryMakeCircleFromEntryExitDirection(startPosition, startVelocity, targets[0] - startPosition, turnCircleRadius, out Circle initialCircle);
        circles.Add(initialCircle);

        // Next the rest of the circles, all based on the approx momentum you'll get while leaving a circle
        Vector3 prevTargetPosition = startPosition;
        for (int target = 0; target < targets.Count; target++)
        {
            Vector3 targetPosition = targets[target];

            TryMakeCircleFromEntryExitDirection(targetPosition, targetPosition - prevTargetPosition, target + 1 < targets.Count ? targets[target + 1] - targetPosition : Vector3.zero, turnCircleRadius, out Circle circle);
            circles.Add(circle);

            prevTargetPosition = targets[target];
        }

        // Next, try pushing away circles that overlap each other
        for (int circle = 1; circle < circles.Count; circle++)
        {
            if (Vector3.Distance(circles[circle].position, circles[circle - 1].position) < circles[circle].radius + circles[circle - 1].radius && circles[circle].clockwise != circles[circle - 1].clockwise)
            {
                // uh oh, the circles, they do overlappeth. and they go in different directions: if they went in the same direction it'd be fine, but when circles go opposite directions we normally criss-cross when we pass between them. no gap means no room to criss-cross
                // What if we _take the circle_, and _push it_ somewhere else?
                Circle adjusted = circles[circle];
                PushCircleAway(circles[circle - 1], ref adjusted, targets[circle - 1]);
                circles[circle] = adjusted;
            }
        }

        // Finally, shrink circles that still overlap
        for (int circle = 1; circle < circles.Count; circle++)
        {
            float circleDistance = Vector3.Distance(circles[circle].position, circles[circle - 1].position);
            if (circleDistance < circles[circle].radius + circles[circle - 1].radius && circles[circle].clockwise != circles[circle - 1].clockwise)
            {
                Circle adjusted = circles[circle];
                adjusted.radius = Mathf.Max(circleDistance - circles[circle - 1].radius, 0f);
                circles[circle] = adjusted;
            }
        }

        // Connect the circles up
        for (int circleIdx = 0; circleIdx < circles.Count - 1; circleIdx++)
        {
            Circle last = circles[circleIdx], next = circles[circleIdx + 1];
            lines.Add(MakeLineSegmentsBetweenCircles(last.position, last.radius, last.clockwise, next.position, next.radius, next.clockwise));
        }
    }

    private List<Line> rotationLines = new List<Line>();
    public float rotationAngl;

    public void PushCircleAway(in Circle sourceCircle, ref Circle targetCircle, Vector3 targetAnchorPoint)
    {
        if (Vector3.Distance(sourceCircle.position, targetCircle.position) < sourceCircle.radius + targetCircle.radius)
        {
            // Circle intersection code stolen from https://discussions.unity.com/t/calulate-the-intersection-points-of-two-circles/728102/3
            float dist = Vector3.Distance(sourceCircle.position, targetAnchorPoint);
            Vector3 c0 = targetAnchorPoint;
            Vector3 c1 = sourceCircle.position;
            float r0 = Vector3.Distance(targetCircle.position, targetAnchorPoint);
            float r1 = sourceCircle.radius + targetCircle.radius;
            float a = (r0 * r0 - r1 * r1 + dist * dist) / (2 * dist);
            float h = Mathf.Sqrt(r0 * r0 - a * a);

            // Find P2.
            double cx2 = c0.x + a * (c1.x - c0.x) / dist;
            double cy2 = c0.z + a * (c1.z - c0.z) / dist;

            // Get the points P3.
            var intersection1 = new Vector3((float)(cx2 + h * (c1.z - c0.z) / dist), 0f, (float)(cy2 - h * (c1.x - c0.x) / dist));
            var intersection2 = new Vector3((float)(cx2 - h * (c1.z - c0.z) / dist), 0f, (float)(cy2 + h * (c1.x - c0.x) / dist));

            targetCircle.position = Vector3.Distance(intersection2, targetCircle.position) < Vector3.Distance(intersection1, targetCircle.position) ? intersection2 : intersection1;
        }
    }

    public void InitTechnique_CirclesWithPredefinedDirections(TestBotExecutor exec)
    {
        Vector3 startPosition = exec.transform.position;
        Vector3 startVelocity = exec.startVelocity;

        // If the current circle is overlapping the last circle and clockwise != last.clockwise
        // Move the circle away and mark it as 'adjusted'
        // If the next circle also overlaps the previous circle and the previous circle is marked as adjusted, shrink the previous circle until it is out of our way

        if (TryMakeCirclesFromVelocity(in startPosition, in startVelocity, turnCircleRadius, out Vector3 initialClockwisePosition, out Vector3 initialAnticlockwisePosition))
        {
            if (exec.targetPositions.Count > 0 && Vector3.Distance(initialClockwisePosition, exec.targetPositions[0]) < Vector3.Distance(initialAnticlockwisePosition, exec.targetPositions[0]))
                circles.Add(new Circle() { position = initialClockwisePosition, radius = turnCircleRadius, clockwise = true });
            else
                circles.Add(new Circle() { position = initialAnticlockwisePosition, radius = turnCircleRadius, clockwise = false });
        }

        Vector3 lastPosition = startPosition;
        for (int targetIdx = 0; targetIdx < exec.targetPositions.Count; targetIdx++)
        {
            if (targetIdx + 1 < exec.targetPositions.Count)
            {
                Vector3 lastToThis = exec.targetPositions[targetIdx] - lastPosition;
                Vector3 thisToNext = exec.targetPositions[targetIdx + 1] - exec.targetPositions[targetIdx];
                const float exitBias = 0.5f;

                if (TryMakeCirclesFromVelocity(exec.targetPositions[targetIdx], Vector3.Slerp(lastToThis, thisToNext, exitBias), turnCircleRadius, out Vector3 clockwisePos, out Vector3 anticlockwisePos))
                {
                    // depends on whether the arm is concave or convex
                    bool shouldBeClockwise = Vector3.Dot(thisToNext, Vector3.Cross(Vector3.up, lastToThis)) > 0f;

                    if (shouldBeClockwise)
                        circles.Add(new Circle() { position = clockwisePos, radius = turnCircleRadius, clockwise = true });
                    else
                        circles.Add(new Circle() { position = anticlockwisePos, radius = turnCircleRadius, clockwise = false });
                }
            }

            // todo set lastPosition = exit point of this circle
            lastPosition = exec.targetPositions[targetIdx];
        }

        for (int circleIdx = 0; circleIdx < circles.Count - 1; circleIdx++)
        {
            Circle last = circles[circleIdx], next = circles[circleIdx + 1];
            lines.Add(MakeLineSegmentsBetweenCircles(last.position, last.radius, last.clockwise, next.position, next.radius, next.clockwise));
        }
    }

    /// <summary>
    /// Returns a circle whose edge overlaps the two points. Returns two positions, one biased towards the right of the vector to pointB from pointA, and the other biased to the left
    /// </summary>
    public static bool TryFitCircleToTwoPoints(in Vector3 pointA, in Vector3 pointB, float circleRadius, out Vector3 clockwisePosition, out Vector3 anticlockwisePosition)
    {
        Vector3 atoB = pointB - pointA;

        float dist = atoB.magnitude;
        if (dist > circleRadius * 2 || dist == 0f)
        {
            clockwisePosition = Vector3.zero;
            anticlockwisePosition = Vector3.zero;
            return false;
        }

        float distProportionOfDiameter = dist / circleRadius / 2f;
        float offsetMag = Mathf.Sqrt(1f - distProportionOfDiameter * distProportionOfDiameter) * circleRadius;
        Vector3 lineCentre = (pointA + pointB) * 0.5f;
        Vector3 offsetVector = Vector3.Cross(atoB, Vector3.up).normalized * offsetMag;
        clockwisePosition = lineCentre + offsetVector;
        anticlockwisePosition = lineCentre - offsetVector;
        return true;
    }

    public static bool TryMakeCirclesFromVelocity(in Vector3 position, in Vector3 velocity, float circleRadius, out Vector3 clockwisePosition, out Vector3 anticlockwisePosition)
    {
        if (velocity.sqrMagnitude == 0f)
        {
            clockwisePosition = Vector3.zero;
            anticlockwisePosition = Vector3.zero;
            return false;
        }

        Vector3 offsetVector = Vector3.Cross(Vector3.up, velocity).normalized * circleRadius;
        clockwisePosition = position + offsetVector;
        anticlockwisePosition = position - offsetVector;
        return true;
    }

    public static bool TryMakeCircleFromEntryExitDirection(in Vector3 position, in Vector3 entryVelocity, in Vector3 exitVelocity, float circleRadius, out Circle circle)
    {
        Vector3 offsetVector = Vector3.Cross(Vector3.up, entryVelocity).normalized;
        float dot = Vector3.Dot(exitVelocity - entryVelocity, offsetVector);
        circle.position = position + offsetVector * (Mathf.Sign(dot != 0 ? dot : 1f) * circleRadius);
        circle.radius = circleRadius;
        circle.clockwise = dot >= 0f;
        return true;
    }

    public static Line MakeLineSegmentsBetweenCircles(in Vector3 circleA, float circleARadius, bool circleAClockwise, in Vector3 circleB, float circleBRadius, bool circleBClockwise)
    {
        Vector3 aToB = circleB - circleA;
        float distanceBetweenCircles = aToB.magnitude;
        float angleTilt = (circleAClockwise ? -circleARadius : circleARadius) + (circleBClockwise ? circleBRadius : -circleBRadius);

        float sin = angleTilt / distanceBetweenCircles, cos = Mathf.Sqrt(1f - sin * sin);
        Vector3 lineDirection = new Vector3(aToB.x * cos - aToB.z * sin, 0f, aToB.x * sin + aToB.z * cos).normalized;
        Vector3 lineOrigin = circleAClockwise ? circleA - new Vector3(lineDirection.z, 0f, -lineDirection.x) * circleARadius : circleA + new Vector3(lineDirection.z, 0f, -lineDirection.x) * circleARadius;

        return new Line() { pointA = lineOrigin, pointB = lineOrigin + lineDirection * Vector3.Dot(aToB, lineDirection) };
    }

    private int targetLineIdx = 0;
    private bool isOnLine = false;

    public override void CalculatePathPoint(in CalculatePathPointParameters parameters, ref CharacterInput input)
    {
        Vector3 targetPosition;

        if (parameters.t >= previewStateTime && parameters.t < previewStateTime + Mathf.Min(parameters.deltaTime, inputInterval))
            Debug.Log($"On line: {isOnLine} tgt {targetLineIdx}");

        var state = parameters.state;
        if (targetLineIdx < lines.Count)
        {
            // Determine whether we're on a circle or on a line
            Vector3 aToB = lines[targetLineIdx].pointB - lines[targetLineIdx].pointA;

            // allow player to miss the point slightly, register if they hit the circle and passed the line dot
            if (!isOnLine && Vector3.Dot(state.position - lines[targetLineIdx].pointA, aToB) > 0f)
            {
                isOnLine = true;
            }
            else if (isOnLine && Vector3.Dot(aToB, state.position - lines[targetLineIdx].pointA) >= Vector3.Dot(aToB, aToB))
            {
                isOnLine = false;
                targetLineIdx++;
            }
        }

        if (isOnLine)
        {
            // When on a line, stick to it until we reach the end
            Vector3 aToB = lines[targetLineIdx].pointB - lines[targetLineIdx].pointA;
            float lookahead = 0.01f;
            Vector3 adjacentPosition = lines[targetLineIdx].pointA + (aToB * Vector3.Dot(aToB, state.position + state.velocity * lookahead - lines[targetLineIdx].pointA) / Vector3.Dot(aToB, aToB)) + aToB.normalized;
            targetPosition = adjacentPosition;
        }
        else
        {
            // When on a circle, turn towards the circle tangent
            if (targetLineIdx < lines.Count)
            {
                Circle currentCircle = circles[targetLineIdx];
                Vector3 toOuter = ((state.position - currentCircle.position).magnitude > 0 ? (state.position - currentCircle.position).normalized : new Vector3(1f, 0f, 0f)) * currentCircle.radius;
                float lookahead = 0.1f;
                float sin = Mathf.Sin(currentCircle.clockwise ? -lookahead * parameters.deltaTime : lookahead * parameters.deltaTime);
                float cos = Mathf.Cos(currentCircle.clockwise ? -lookahead * parameters.deltaTime : lookahead * parameters.deltaTime);
                targetPosition = currentCircle.position + new Vector3(toOuter.x * cos - toOuter.z * sin, 0f, toOuter.x * sin + toOuter.z * cos);
            }
            else
                targetPosition = state.position;
        }

        float desiredDirectionRightness = Vector3.Dot(Vector3.Cross(Vector3.up, state.velocity).normalized, targetPosition - state.position);
        float rotation = desiredDirectionRightness > 0f ? steerStrengthDegrees : -steerStrengthDegrees;
        input.worldMovementDirection = state.velocity.RotatedAroundY(rotation).normalized;
    }

    public override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.color = Color.blue;
        foreach (var circle in circles)
        {
            Gizmos.color = circle.clockwise ? Color.yellow : Color.blue;
            GizmoExtensions.DrawCircle(circle.position + new Vector3(0f, 0.05f, 0f), circle.radius);
        }
        Gizmos.color = Color.blue;
        foreach (var line in lines)
            Gizmos.DrawLine(line.pointA + new Vector3(0f, 0.05f, 0f), line.pointB + new Vector3(0f, 0.05f, 0f));

        Gizmos.color = Color.cyan;
        foreach (var line in rotationLines)
            Gizmos.DrawLine(line.pointA, line.pointB);
    }

    public void Update(BotTaskParams taskParams, ref CharacterInput input)
    {
    }
}
