using UnityEngine;

public class DodgeTrainingBot : TrainingBotBase
{
    TrainingRingSpawner spawner;

    private float closestRingDistanceTotal = float.MaxValue;

    private double avgDist = 0f;
    private double avgSamples = 0f;

    // Start is called before the first frame update
    protected override void Start()
    {
        base.Start();
        spawner = FindObjectOfType<TrainingRingSpawner>();
    }

    public override void OnReset()
    {
        base.OnReset();
        closestRingDistanceTotal = float.MaxValue;
        avgDist = 0;
        avgSamples = 0;
    }

    public override bool OnTick(float deltaTime, float substepBase, ref PlayerInput charInput)
    {
        float closest = float.MaxValue;
        TrainingRingSpawner.Ring closestRing = default;
        float sClosest = float.MaxValue;
        TrainingRingSpawner.Ring sClosestRing = default;
        for (int i = 0; i < spawner.spawnedRings.Count; i++)
        {
            float dist = Vector3.Distance(spawner.spawnedRings[i].position + spawner.spawnedRings[i].speed * substepBase, transform.position);

            /*if (dist < 5f)
            {
                Vector3 dir = spawner.spawnedRings[i].speed.normalized;
                Vector3 pos = spawner.spawnedRings[i].position;
                float dot = Vector3.Dot(dir, transform.position - pos);
                if (dot >= -1f && dot <= spawner.spawnedRings[i].speed.magnitude * Time.deltaTime + 1f && Vector3.Distance(transform.position.AlongPlane(dir), pos.AlongPlane(dir)) <= 1f)
                    movement.velocity = Vector3.zero;
            }*/

            if (dist < closest)
            {
                closest = dist;
                closestRing = spawner.spawnedRings[i];
            }
            else if (dist < sClosest)
            {
                sClosest = dist;
                sClosestRing = spawner.spawnedRings[i];
            }
        }

        avgDist += closest;
        avgSamples += 1;

        networkInput[0] = movement.velocity.x;
        networkInput[1] = movement.velocity.z;
        networkInput[2] = movement.transform.position.x - (closestRing.position.x + closestRing.speed.x * substepBase);
        networkInput[3] = movement.transform.position.z - (closestRing.position.z + closestRing.speed.z * substepBase);
        networkInput[4] = movement.transform.position.x - (sClosestRing.position.x + sClosestRing.speed.x * substepBase);
        networkInput[5] = movement.transform.position.z - (sClosestRing.position.z + sClosestRing.speed.z * substepBase);

        float[] output = network.FeedForward(networkInput);

        Vector2 moveDir = new Vector2(output[0], 1f).normalized;

        charInput.moveHorizontalAxis = moveDir.x;
        charInput.moveVerticalAxis = moveDir.y;

        // output should be side movement amount
        return true;
    }

    public override float GetFitness()
    {
        return transform.position.z + (float)(avgDist / avgSamples) * 3;
    }
}
