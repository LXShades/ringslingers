using System.Collections.Generic;
using UnityEngine;

public class CheckpointTrainingEnvironment : TrainingEnvironmentBase
{
    [Header("Checkpoints")]
    public GameObject checkpointPrefab;
    public int numCheckpoints = 5;
    public bool doRandomizeCheckpoints = true;
    public float checkpointSpawnRadius = 5f;
    public bool spawnCheckpointsOnEdge = false;
    public float checkpointRadius = 2f;
    public float checkpointRandomHeightRange = 0f;
    public int numLines = 2;
    public int lineNumCheckpoints = 3;
    public float lineSpacing = 1f;

    public List<Transform> checkpoints { get; private set; } = new List<Transform>();

    private void Start()
    {
        for (int i = 0; i < numCheckpoints; i++)
        {
            GameObject checkpoint = Instantiate(checkpointPrefab);
            checkpoints.Add(checkpoint.transform);
        }
    }

    public override void OnCycle()
    {
        // Setup checkpoints
        if (!doRandomizeCheckpoints)
            Random.InitState(0);

        for (int i = 0; i < checkpoints.Count; i++)
        {
            Vector2 circle = Random.insideUnitCircle;
            if (spawnCheckpointsOnEdge)
                circle.Normalize();
            checkpoints[i].transform.position = new Vector3(circle.x, Random.Range(0f, checkpointRandomHeightRange) / checkpointSpawnRadius, circle.y) * checkpointSpawnRadius;
            checkpoints[i].transform.localScale = Vector3.one * (checkpointRadius * 2);
            checkpoints[i].GetComponent<MeshRenderer>().material.color = i == 0 ? Color.green : Color.red;
        }

        for (int i = 0; i < numLines; i++)
        {
            int lineStart = Random.Range(0, Mathf.Min(numCheckpoints - lineNumCheckpoints));
            Vector3 lineDirection = Random.insideUnitCircle.normalized;
            lineDirection.z = lineDirection.y;
            lineDirection.y = 0f;

            for (int j = lineStart; j < lineStart + lineNumCheckpoints; j++)
            {
                checkpoints[j].transform.position = checkpoints[lineStart].transform.position + lineDirection * ((j - lineStart) * lineSpacing);
            }
        }
    }
}
