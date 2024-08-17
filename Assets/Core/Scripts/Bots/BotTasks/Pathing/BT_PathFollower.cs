using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BT_PathFollower : ITestableBotTask, IBotTask
{
    public Vector3[] targets;
    public int currentTargetIndex = 0;
    public Vector3 startVelocity;

    private float characterHalfHeight = 0.5f;

    public float targetHorizontalRadius;
    private float targetVerticalRadius = 2f;

    private Vector3 previousPosition;
    private Vector3 previousVelocity;

    public void SetupPath(IReadOnlyCollection<Vector3> targets, Vector3 startVelocity, float targetRadius)
    {
        this.targets = targets.ToArray();
        this.startVelocity = startVelocity;
        this.targetHorizontalRadius = targetRadius;
        this.currentTargetIndex = 0;
    }

    public virtual void InitTests(TestBotExecutor exec)
    {
        targets = exec.targetPositions.ToArray();
        startVelocity = exec.startVelocity;
        targetHorizontalRadius = exec.targetRadius;
        currentTargetIndex = 0;
    }

    public virtual void Init(in BotTaskParams taskParams)
    {
        previousPosition = taskParams.position;
        previousVelocity = taskParams.movement.velocity;
    }

    public virtual void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        if (currentTargetIndex < targets.Length && previousVelocity.sqrMagnitude > 0f)
        {
            Vector3 positionClosestToTarget = taskParams.position;
            Vector3 previousVelocityNormalized = previousVelocity.normalized;
            float targetDot = Vector3.Dot(previousVelocityNormalized, targets[currentTargetIndex]);
            float lastDot = Vector3.Dot(previousVelocityNormalized, previousPosition);
            float nextDot = Vector3.Dot(previousVelocityNormalized, taskParams.position);

            if (lastDot < targetDot && nextDot >= 0f)
            {
                // we zoomed past the point, see if we passed closely enough
                positionClosestToTarget = Vector3.Lerp(previousPosition, taskParams.position, (targetDot - lastDot) / (nextDot - lastDot));
            }


            float horDist = VectorExtensions.HorizontalDistance(positionClosestToTarget, targets[currentTargetIndex]);
            float vertDist = Mathf.Abs(positionClosestToTarget.y + characterHalfHeight - targets[currentTargetIndex].y);
            if (horDist < targetHorizontalRadius && vertDist <= targetVerticalRadius)
            {
                currentTargetIndex++;
            }
        }
    }

    public virtual void OnDrawGizmos()
    {
    }
}
