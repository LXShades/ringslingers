using UnityEngine;

[System.Serializable]
public class BT_TargetVelocityBasedPathFollower : BT_PathFollower
{
    private TestBotExecutor exec;

    public float interval = 0.2f;

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

        if (currentTargetIndex >= targets.Length)
            return;

        time += taskParams.deltaTime;

        Vector3 naturalPositionAfterInterval = taskParams.position + taskParams.movement.velocity * interval;
        Vector3 desiredPosition = taskParams.position + (targets[currentTargetIndex] - taskParams.position).normalized * (taskParams.movement.velocity.magnitude * interval);
        Vector3 accelerationPossibility = VectorExtensions.HorizontalNormalized(desiredPosition - naturalPositionAfterInterval) * (taskParams.movement.CalculateAccelerationMagnitude(taskParams.movement.velocity.Horizontal(), time) * interval);

        if (time <= exec.watchTime && time + taskParams.deltaTime > exec.watchTime)
        {
            // draw the things
            DebugDraw.DrawLine(taskParams.position, naturalPositionAfterInterval, DebugDraw.Style.Thick.Color(Color.blue));
            DebugDraw.DrawLine(taskParams.position, desiredPosition, DebugDraw.Style.Thick.Color(Color.green));
            DebugDraw.DrawLine(naturalPositionAfterInterval, naturalPositionAfterInterval + accelerationPossibility, DebugDraw.Style.Thick.Color(Color.yellow));
        }

        //input.worldMovementDirection = accelerationPossibility.normalized * Mathf.Min(Vector3.Distance(naturalPositionAfterInterval, desiredPosition) / accelerationPossibility.magnitude, 1f);
        input.worldMovementDirection = Vector3.ClampMagnitude((desiredPosition - taskParams.position).normalized + accelerationPossibility.normalized * Mathf.Min(Vector3.Distance(naturalPositionAfterInterval, desiredPosition) / accelerationPossibility.magnitude, 1f), 1f);
    }
}
