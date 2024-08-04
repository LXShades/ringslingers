using System.Collections.Generic;
using UnityEngine;

public class TestBotAction_CircleBasedTurning : TestBotAction_WithPreCalculatedPath
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

    public override void Init(TestBotExecutor exec)
    {
        base.Init(exec);

        circles.Clear();
        lines.Clear();

        Vector3 startPosition = exec.transform.position;
        Vector3 startVelocity = exec.startVelocity;

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
                if (TryFitCircleToTwoPoints(exec.targetPositions[targetIdx], exec.targetPositions[targetIdx + 1], turnCircleRadius, out Vector3 fitLeftPos, out Vector3 fitRightPos) && false)
                {
                    if (Vector3.Distance(fitLeftPos, lastPosition) < Vector3.Distance(fitRightPos, lastPosition))
                        circles.Add(new Circle() { position = fitLeftPos, radius = turnCircleRadius, clockwise = false });
                    else
                        circles.Add(new Circle() { position = fitRightPos, radius = turnCircleRadius, clockwise = true });
                }
                else
                {
                    Vector3 lastToThis = exec.targetPositions[targetIdx] - lastPosition;
                    Vector3 thisToNext = exec.targetPositions[targetIdx + 1] - exec.targetPositions[targetIdx];
                    bool entryBias = false; 

                    if (TryMakeCirclesFromVelocity(exec.targetPositions[targetIdx], entryBias ? lastToThis : thisToNext, turnCircleRadius, out Vector3 clockwisePos, out Vector3 anticlockwisePos))
                    {
                        // depends on whether the arm is concave or convex
                        bool shouldBeClockwise = Vector3.Dot(thisToNext, Vector3.Cross(Vector3.up, lastToThis)) > 0f;

                       /* if (circles.Count > 0)
                        {
                            // If we overlap one of the last circles, try changing the entry bias. If that doesn't work, TODO, we're screwed I guess.
                            Circle lastCircle = circles[circles.Count - 1];
                            float overlapSeverity = turnCircleRadius + lastCircle.radius - Vector3.Distance(lastCircle.position, shouldBeClockwise ? clockwisePos : anticlockwisePos);
                            if (overlapSeverity > 0f)
                            {
                                if (TryMakeCirclesFromVelocity(exec.targetPositions[targetIdx], !entryBias ? lastToThis : thisToNext, turnCircleRadius, out Vector3 newClockwisePos, out Vector3 newAnticlockwisePos))
                                {
                                    if (turnCircleRadius + lastCircle.radius - Vector3.Distance(lastCircle.position, shouldBeClockwise ? clockwisePos : anticlockwisePos) < overlapSeverity)
                                    {
                                        anticlockwisePos = newAnticlockwisePos;
                                        clockwisePos = newClockwisePos;
                                    }
                                }
                            }
                        }*/

                        if (shouldBeClockwise)
                            circles.Add(new Circle() { position = clockwisePos, radius = turnCircleRadius, clockwise = true });
                        else
                            circles.Add(new Circle() { position = anticlockwisePos, radius = turnCircleRadius, clockwise = false });
                    }
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

    public override void CalculatePathPoint(TestBotExecutor exec, ref CharacterInput input, float t, in CharacterState state, int currentTarget)
    {

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
    }
}
