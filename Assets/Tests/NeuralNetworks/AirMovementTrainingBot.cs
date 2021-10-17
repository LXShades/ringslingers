using UnityEngine;

public class AirMovementTrainingBot : TrainingBotBase
{
    private Transform target;
    private Transform nextTarget;

    private CheckpointTrainingEnvironment environment => trainingEnvironment as CheckpointTrainingEnvironment;

    private int currentCheckpoint = 0;

    public override void OnReset()
    {
        base.OnReset();

        target = environment.checkpoints[0];
        nextTarget = environment.checkpoints[1];
        currentCheckpoint = 0;
    }

    public override bool OnTick(float deltaTime, float substepBase, ref PlayerInput charInput)
    {
        if (target != null && Vector3.Distance(transform.position, target.position) < environment.checkpointRadius)
        {
            target.GetComponent<MeshRenderer>().material.color = Color.blue;
            currentCheckpoint++;
            target = currentCheckpoint < environment.checkpoints.Count ? environment.checkpoints[currentCheckpoint] : null;
            nextTarget = currentCheckpoint + 1 < environment.checkpoints.Count ? environment.checkpoints[currentCheckpoint + 1].transform : null;
        }

        if (target == null/* || nextTarget == null*/)
        {
            charInput.moveHorizontalAxis = 0;
            charInput.moveVerticalAxis = 0;
            return true; // stop providing inputs, encourage bot to stop before landing
        }

        networkInput[0] = target.position.x - transform.position.x;
        networkInput[1] = target.position.y - transform.position.y;
        networkInput[2] = target.position.z - transform.position.z;
        networkInput[3] = movement.velocity.x;
        networkInput[4] = movement.velocity.y;
        networkInput[5] = movement.velocity.z;

        float[] output = network.FeedForward(networkInput);
        Vector2 dir = new Vector2(output[0], output[1]).normalized;

        charInput.moveHorizontalAxis = dir.x;
        charInput.moveVerticalAxis = dir.y;
        return true;
    }

    public override float GetFitness()
    {
        float fitness = currentCheckpoint;

        if (target != null)
        {
            if (currentCheckpoint - 1 >= 0 && currentCheckpoint - 1 < environment.checkpoints.Count)
                fitness += 1f - Vector3.Distance(transform.position, target.position) / Vector3.Distance(environment.checkpoints[currentCheckpoint - 1].position, target.position);
            else if (currentCheckpoint == 0)
                fitness += 1f - Vector3.Distance(transform.position, target.position) / Vector3.Distance(target.position, startPosition);
        }
        else
        {
            if (environment.checkpoints.Count == 1 && currentCheckpoint == 1)
            {
                fitness += 1f - Vector3.Distance(transform.position, environment.checkpoints[0].position) / Vector3.Distance(environment.checkpoints[0].position, startPosition);
            }
        }

        return fitness;
    }
}
