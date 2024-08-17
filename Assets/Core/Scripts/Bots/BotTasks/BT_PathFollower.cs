using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BT_PathFollower : ITestableBotTask, IBotTask
{
    public Vector3[] targets;
    public Vector3 startVelocity;
    public float targetRadius;

    public void SetupPath(IReadOnlyCollection<Vector3> targets, Vector3 startVelocity, float targetRadius)
    {
        this.targets = targets.ToArray();
        this.startVelocity = startVelocity;
        this.targetRadius = targetRadius;
    }

    public virtual void InitTests(TestBotExecutor exec)
    {
        targets = exec.targetPositions.ToArray();
        startVelocity = exec.startVelocity;
        targetRadius = exec.targetRadius;
    }

    public virtual void Init(in BotTaskParams taskParams)
    {
    }

    public virtual void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
    }

    public virtual void OnDrawGizmos()
    {
    }
}
