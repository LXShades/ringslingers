using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BT_PrecalculatedPathFollower : ITestableBotTask, IBotTask
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
    public Vector3[] targets;
    public Vector3 startVelocity;
    public float pathDuration = 5f;
    public float targetRadius = 0.5f;
    public float previewStateTime = -1f;

    private int playbackInputFrame = 0;


    public virtual void InitTests(TestBotExecutor exec)
    {
        targets = exec.targetPositions.ToArray();
        startVelocity = exec.startVelocity;
        pathDuration = exec.simulationDuration;
        targetRadius = exec.targetRadius;
    }

    public virtual void Init(in BotTaskParams taskParams)
    {
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

    public void Update(in BotTaskParams taskParams, ref CharacterInput input)
    {
        if (playbackInputFrame < inputs.Count)
            input = inputs[playbackInputFrame++];
    }

    public virtual void CalculatePathPoint(in CalculatePathPointParameters parameters, ref CharacterInput input) { }

    public virtual void OnDrawGizmos()
    {
    }
}