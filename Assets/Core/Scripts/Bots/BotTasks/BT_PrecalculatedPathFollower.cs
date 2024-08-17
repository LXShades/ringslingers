using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BT_PrecalculatedPathFollower : BT_PathFollower
{
    public struct CalculatePathPointParameters
    {
        public float t;
        public float deltaTime;
        public CharacterState state;
        public int currentTarget;
    }

    public float inputInterval = 0.2f;

    private List<CharacterInput> inputs = new List<CharacterInput>();
    public float pathDuration = 5f;
    public float previewStateTime = -1f;

    private int playbackInputFrame = 0;

    public override void InitTests(TestBotExecutor exec)
    {
        base.InitTests(exec);
        targetRadius = exec.targetRadius;
    }

    public override void Init(in BotTaskParams taskParams)
    {
        base.Init(in taskParams);

        playbackInputFrame = 0;
        inputs.Clear();

        int currentTarget = 0;
        CharacterState state = new CharacterState() { position = taskParams.characterObject.transform.position, velocity = startVelocity };
        CharacterInput input = default;
        float lastInputChangeTime = -inputInterval - 1f;

        for (float t = 0f; t < pathDuration; t += taskParams.deltaTime)
        {
            if (currentTarget >= targets.Length)
            {
                break;
            }

            if (t - lastInputChangeTime >= inputInterval)
            {
                CalculatePathPoint(new CalculatePathPointParameters() { currentTarget = currentTarget, t = t, deltaTime = taskParams.deltaTime, state = state }, ref input);
                lastInputChangeTime = t;
            }

            inputs.Add(input);
            taskParams.movement.RunSimpleCollisionFreeSimulation(ref state, input, taskParams.deltaTime);

            if (VectorExtensions.HorizontalDistance(state.position, targets[currentTarget]) < targetRadius)
                currentTarget++;
        }
    }

    public override void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        base.Update(in taskParams, ref input);
        if (playbackInputFrame < inputs.Count)
            input = inputs[playbackInputFrame++];
    }

    public virtual void CalculatePathPoint(in CalculatePathPointParameters parameters, ref CharacterInput input) { }

    public virtual void OnDrawGizmos()
    {
    }
}