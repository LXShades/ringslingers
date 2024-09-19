using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public struct JumpHeightToTimeCalculator
{
    public float heightOffset;
    public float jumpSpeed;
    public float gravity;

    public float timeA;
    public float timeB;
    public bool canReachHeight;
    public float closestHeightReached;

    public void Calculate()
    {
        // Quadratic stuff
        float innerComponents = jumpSpeed * jumpSpeed - 2 * gravity * heightOffset;

        if (innerComponents < 0)
        {
            canReachHeight = false;
        }
        else
        {
            canReachHeight = true;

            float sqrtStuff = Mathf.Sqrt(innerComponents);
            timeA = (jumpSpeed - sqrtStuff) / (gravity);
            timeB = (jumpSpeed + sqrtStuff) / (gravity);
        }
    }
}

public class PathFilter_JumpGaps : PathFilter
{
    public Color runwayPointColour = Color.magenta;
    private float heightDifferenceJumpThreshold = 0.2f;
    private float turnReductionThreshold = 0.5f;

    private int jumpTargetPoint;

    public override void Apply(List<PathPoint> points)
    {
        base.Apply(points);

        jumpTargetPoint = -1;

        for (int idx = 1; idx < points.Count - 1; idx++)
        {
            // todo: an actual way to check for jump links..... THIS WOULDN'T BE A PROBLEM IF UNITY WASN'T UNITYING
            if (points[idx + 1].position.y >= points[idx].position.y + heightDifferenceJumpThreshold)
            {
                // check if it lines up with the previous velocity
                Vector3 prevVelocity = (points[idx].position - points[idx - 1].position).HorizontalNormalized();
                Vector3 thisVelocity = (points[idx + 1].position - points[idx].position).HorizontalNormalized();

                if (Vector3.Dot(prevVelocity.normalized, thisVelocity.normalized) < 1f - turnReductionThreshold)
                {
                    // Try and make an intermediate point so the player can approach the next point with a good velocity
                    // todo: we need to make sure it's on a valid collision surface. iterate until a good balance is hit
                    // todo: customisable thresholds, sensible thresholds, where are we getting this data from anyway?
                    points.Insert(idx, new PathPoint(points[idx].position - thisVelocity * 2f - prevVelocity * 3f)
                    {
                        debugColor = runwayPointColour
                    });
                    idx += 1;
                }
            }
        }
    }

    public override void Update(in PathPointTaskParams taskParams, ref CharacterInput input)
    {
        base.Update(taskParams, ref input);

        PlayerCharacterMovement movement = taskParams.botTaskParams.movement;

        // NOTE: ASSUMES PATH POINTS TO BE ON THE GROUND
        if (taskParams.currentPathPoint + 2 < taskParams.pathPoints.Count)
        {
            bool hasPrevHit = NavMesh.SamplePosition(taskParams.pathPoints[taskParams.currentPathPoint], out NavMeshHit prevHit, 20f, ~0);
            bool hasNextHit = NavMesh.SamplePosition(taskParams.pathPoints[taskParams.currentPathPoint + 1], out NavMeshHit nextHit, 20f, ~0);

            if (hasPrevHit && hasNextHit)
            {
                bool hasLink = BotNavMeshBuilder.TryGetNavLink(taskParams.pathPoints[taskParams.currentPathPoint], taskParams.pathPoints[taskParams.currentPathPoint + 1], out BotNavMeshBuilder.NavLink link);

                //if (hasLink)
                if (nextHit.position.y >= prevHit.position.y + heightDifferenceJumpThreshold) // temp
                {
                    // A nav link is evidence that this area can be jumped between
                    // Draw a target using our calculator, see if they match well
                    JumpHeightToTimeCalculator jumpHeightToTime = new JumpHeightToTimeCalculator()
                    {
                        jumpSpeed = movement.jumpSpeed,
                        gravity = movement.gravity,
                        heightOffset = nextHit.position.y - taskParams.botTaskParams.position.y
                    };

                    jumpHeightToTime.Calculate();

                    if (taskParams.botTaskParams.movement.isOnGround && !taskParams.botTaskParams.lastInput.btnJump)
                    {
                        if (jumpHeightToTime.canReachHeight)
                        {
                            Vector3 positionAtJumpedHeight = taskParams.botTaskParams.position + movement.velocity * jumpHeightToTime.timeB;
                            bool couldPassPoint = Vector3.Dot(positionAtJumpedHeight - taskParams.pathPoints[taskParams.currentPathPoint + 1].position,
                                                              taskParams.pathPoints[taskParams.currentPathPoint + 1].position - taskParams.pathPoints[taskParams.currentPathPoint].position) > 0f;

                            input.btnJump |= couldPassPoint;
                            if (couldPassPoint)
                                jumpTargetPoint = taskParams.currentPathPoint + 1;

                            DebugDraw.DrawArrow(taskParams.botTaskParams.position, taskParams.botTaskParams.position + (couldPassPoint ? new Vector3(0f, 2f, 0f) : new Vector3(0f, 0.5f, 0f)), couldPassPoint ? new Color(0.2f, 0.7f, 0f) : new Color(0.5f, 0.5f, 0f));
                            if (couldPassPoint)
                                DebugDraw.DrawCross(taskParams.botTaskParams.position + movement.velocity.Horizontal() * jumpHeightToTime.timeB + new Vector3(0f, jumpHeightToTime.heightOffset, 0f), 2f, Color.green);
                        }
                    }
                }



                if ((movement.state & CharacterMovementState.Jumped) != 0 && jumpTargetPoint != -1)
                {
                    // get time til landing on the next target point
                    // target velocity should aim to land squarely on that point i.e. position + velocity * t = targetPosition or velocity = (targetPosition - position) / t
                    JumpHeightToTimeCalculator timeTilLanding = new JumpHeightToTimeCalculator()
                    {
                        jumpSpeed = movement.velocity.y,
                        gravity = movement.gravity,
                        heightOffset = taskParams.pathPoints[jumpTargetPoint].y - taskParams.botTaskParams.position.y
                    };

                    timeTilLanding.Calculate();

                    // try and reach a target velocity to land on the point
                    float accelMultiplier = movement.airAccelerationMultiplier;

                    float interval = 0.1f;
                    Vector3 naturalPositionAfterInterval = taskParams.botTaskParams.position + taskParams.botTaskParams.movement.velocity * interval;

                    float accelMagnitude = movement.CalculateAccelerationMagnitude(movement.velocity.Horizontal(), interval) * accelMultiplier;
                    //Vector3 desiredVelocity = VectorExtensions.HorizontalNormalized(targetPositionToUse - naturalPositionAfterInterval) * Mathf.Min(movement.topSpeed, movement.velocity.magnitude + accelMagnitude);
                    Vector3 desiredVelocity = (taskParams.pathPoints[jumpTargetPoint] - taskParams.botTaskParams.position) / timeTilLanding.timeB;

                    input.worldMovementDirection = Vector3.ClampMagnitude((desiredVelocity - movement.velocity) / accelMagnitude, 1f);
                }
            }
        }

        if ((movement.state & CharacterMovementState.Jumped) == 0 && input.btnJump == false)
            jumpTargetPoint = -1;

        // Hold jump while in the air
        // Todo: release jump at the optimum time
        input.btnJump |= (movement.state & CharacterMovementState.Jumped) != 0;
    }
}
