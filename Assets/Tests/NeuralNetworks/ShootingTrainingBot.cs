using UnityEngine;

public class ShootingTrainingBot : TrainingBotBase
{
    public float fireSpeed = 32.81f;
    Vector3 firePosition;
    Vector3 fireDirection = Vector3.zero;
    float closestDist = float.MaxValue;

    protected override void Start()
    {
        base.Start();

        if (localPlayer != null)
        {
            networkInput[0] = localPlayer.transform.position.x - transform.position.x;
            networkInput[1] = localPlayer.transform.position.z - transform.position.z;
            networkInput[2] = localPlayer.velocity.x;
            networkInput[3] = localPlayer.velocity.z;
            networkInput[4] = Vector3.Distance(localPlayer.transform.position.Horizontal(), transform.position.Horizontal());

            float[] output = network.FeedForward(networkInput);

            firePosition = transform.position;
            fireDirection = new Vector3(output[0], 0, output[1]).normalized;
        }
    }

    public override bool OnTick(float deltaTime, float substepBase, ref PlayerInput charInput)
    {
        if (Vector3.Distance(firePosition, transform.position) > Vector3.Distance(transform.position, localPlayer.transform.position))
        {
            trainer.StartCycle();
            return false;
        }

        firePosition += fireDirection * (deltaTime * fireSpeed);

        if (localPlayer != null)
            closestDist = Mathf.Min(Vector3.Distance(firePosition, localPlayer.transform.position.Horizontal()), closestDist);
        return true;
    }

    public override float GetFitness()
    {
        return -closestDist;
    }

    public override void OnReset()
    {
        Start();
        closestDist = float.MaxValue;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(firePosition, 0.25f);
    }
}
