using UnityEngine;

[System.Serializable]
public class BT_TargetVelocityBasedPathFollower : BT_PathFollower
{
    private TestBotExecutor exec;

    public float interval = 0.04f;
    public float cornerBlendRange = 2f;

    private float time;

    public override void InitTests(TestBotExecutor exec)
    {
        this.exec = exec;
        base.InitTests(exec);
    }

    public override void Init(in BotTaskParams taskParams)
    {
        base.Init(in taskParams);
        time = 0f;
    }

    public override void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        base.Update(in taskParams, ref input);

        if (currentTargetIndex >= targets.Count)
            return;

        time += taskParams.deltaTime;

        // todo: move this into movement function
        float accelMultiplier = 1f;
        if (!taskParams.movement.isOnGround)
            accelMultiplier *= taskParams.movement.airAccelerationMultiplier;
        if (taskParams.movement.state == CharacterMovementState.Rolling)
            accelMultiplier *= taskParams.movement.rollingAccelerationMultiplier;
        if (taskParams.movement.isInWater)
            accelMultiplier *= taskParams.movement.waterSpeedMultiplier;

        // even if heading towards our last position, we might nudge it a bit in case our prediction goes _past_ it, which is okay, that will happen as we get close

        Vector3 naturalPositionAfterInterval = taskParams.position + taskParams.movement.velocity * interval;

        // Adjust the target position to chase if we're passing through a point, otherwise we'll try to slow ourselves down as we reach it, which is not desirable
        bool willHitTarget = HasHitTarget(taskParams.position, naturalPositionAfterInterval, targets[currentTargetIndex]);
        Vector3 targetPositionToUse = currentTargetIndex < targets.Count - 1 ?
            (willHitTarget ? targets[currentTargetIndex + 1] : targets[currentTargetIndex])
            : (willHitTarget ? targetPositionToUse = taskParams.position + taskParams.movement.velocity : targets[currentTargetIndex]);
        // old - this worked, annoyingly, and I don't know why
        //Vector3 accelerationPossibility = VectorExtensions.HorizontalNormalized(desiredPosition - naturalPositionAfterInterval) * (taskParams.movement.CalculateAccelerationMagnitude(taskParams.movement.velocity.Horizontal(), interval) * interval);
        float accelMagnitude = taskParams.movement.CalculateAccelerationMagnitude(taskParams.movement.velocity.Horizontal(), interval) * accelMultiplier;
        Vector3 desiredVelocity = VectorExtensions.HorizontalNormalized(targetPositionToUse - naturalPositionAfterInterval) * Mathf.Min(taskParams.movement.topSpeed, taskParams.movement.velocity.magnitude + accelMagnitude);

        if (taskParams.isWatchTime)
        {
            // draw the things
            DebugDraw.DrawLine(taskParams.position, naturalPositionAfterInterval, DebugDraw.Style.Thick.Color(new Color(0f, 0f, 1f, 0.2f)));
            DebugDraw.DrawLine(taskParams.position, taskParams.position + (desiredVelocity - taskParams.movement.velocity).normalized, DebugDraw.Style.Thick.Color(Color.yellow));
        }

        // i swear teach I got dis
        // velocity + ? * accelMagnitude = desiredVelocity
        // ? * accelMagnitude + velocity = desiredVelocity
        // ? * accelMagnitude = desiredVelocity - velocity
        // ? = (desiredVelocity - velocity) / (accelMagnitude)

        //input.worldMovementDirection = Vector3.ClampMagnitude((desiredPosition - taskParams.position).normalized + accelerationPossibility.normalized * Mathf.Min(Vector3.Distance(naturalPositionAfterInterval, desiredPosition) / accelerationPossibility.magnitude, 1f), 1f);
        input.worldMovementDirection = Vector3.ClampMagnitude((desiredVelocity - taskParams.movement.velocity) / accelMagnitude, 1f);
    }
}
