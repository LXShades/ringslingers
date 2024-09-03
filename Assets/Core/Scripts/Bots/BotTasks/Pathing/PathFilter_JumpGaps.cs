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
    private bool isJumpPressed = false;

    public override void Apply(List<PathPoint> points)
    {
        base.Apply(points);
        isJumpPressed = false;
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
                if (nextHit.position.y > prevHit.position.y + 0.2f) // temp
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

                    if (taskParams.botTaskParams.movement.isOnGround && !input.btnJump)
                    {
                        if (jumpHeightToTime.canReachHeight)
                        {
                            Vector3 positionAtJumpedHeight = taskParams.botTaskParams.position + movement.velocity * jumpHeightToTime.timeB;
                            bool couldPassPoint = Vector3.Dot(positionAtJumpedHeight - taskParams.pathPoints[taskParams.currentPathPoint + 1].position,
                                                              taskParams.pathPoints[taskParams.currentPathPoint + 1].position - taskParams.pathPoints[taskParams.currentPathPoint].position) > 0f;

                            input.btnJump |= couldPassPoint;

                            DebugDraw.DrawArrow(taskParams.botTaskParams.position, taskParams.botTaskParams.position + (couldPassPoint ? new Vector3(0f, 2f, 0f) : new Vector3(0f, 0.5f, 0f)), couldPassPoint ? new Color(0.2f, 0.7f, 0f) : new Color(0.5f, 0.5f, 0f));
                            if (couldPassPoint)
                                DebugDraw.DrawCross(taskParams.botTaskParams.position + movement.velocity.Horizontal() * jumpHeightToTime.timeB + new Vector3(0f, jumpHeightToTime.heightOffset, 0f), 2f, Color.green);
                        }
                    }

                    // Hold jump while in the air
                    // Todo: release jump at the optimum time
                    input.btnJump |= !taskParams.botTaskParams.movement.isOnGround;
                }
            }
        }
    }
}
