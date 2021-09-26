using System.Collections.Generic;
using UnityEngine;

public class NeuralTrainer : MonoBehaviour
{
    [Header("Spawning")]
    public int populationSize;
    public TrainingBotBase botPrefab;
    public GameObject checkpointPrefab;

    [Header("Local test player")]
    public PlayerCharacterMovement localPlayer;
    public bool resetLocalPlayerOnCycle = true;

    [Header("Evolution")]
    [Range(0.0001f, 1f)] public float mutationChance = 0.01f;
    [Range(0f, 1f)] public float mutationStrength = 0.5f;

    [Header("Simulation")]
    public float timeframe;
    [Range(0.1f, 10f)] public float gameSpeed = 1f;

    [Header("Checkpoints")]
    public int numCheckpoints = 5;
    public bool doRandomizeCheckpoints = true;
    public float checkpointSpawnRadius = 5f;
    public bool spawnCheckpointsOnEdge = false;
    public float checkpointRadius = 2f;
    public float checkpointRandomHeightRange = 0f;

    [Header("Speed")]
    public float startVerticalSpeed = 15f;
    public float maxStartHorizontalSpeed = 8f;

    [Header("Save/load")]
    public bool saveResults = true;
    public string networkName = "";
    public string loadAsString = "";

    public System.Action onCycle { get; set; }

    public string fullNetworkNameBackup => $"Assets/{networkName}_{botPrefab?.networkName}_{System.DateTime.Now.ToString("s", System.Globalization.DateTimeFormatInfo.InvariantInfo).Replace(":", "-")}.txt";
    public string fullNetworkName => $"Assets/{networkName}_{botPrefab?.networkName}.txt";

    public List<NeuralNetwork> networks { get; private set; }
    private List<TrainingBotBase> bots = new List<TrainingBotBase>();
    private List<Transform> checkpoints = new List<Transform>();

    private float lastRunTime = -99f;

    void Start()
    {
        if (populationSize % 2 != 0)
            populationSize = (populationSize + 1) / 2 * 2;

        for (int i = 0; i < numCheckpoints; i++)
        {
            GameObject checkpoint = Instantiate(checkpointPrefab);
            checkpoints.Add(checkpoint.transform);
        }

        InitNetworks();
    }

    private void Update()
    {
        Time.timeScale = gameSpeed;

        if (Time.time - lastRunTime >= timeframe)
            StartCycle();
    }

    public void InitNetworks()
    {
        if (botPrefab == null)
        {
            Debug.LogError("Cannot init networks: no bot prefab");
            throw new System.ArgumentNullException();
        }

        networks = new List<NeuralNetwork>();
        for (int i = 0; i < populationSize; i++)
        {
            NeuralNetwork net = new NeuralNetwork(botPrefab.layers);

            if (!string.IsNullOrEmpty(loadAsString))
                net.LoadAsString(loadAsString);
            else if (System.IO.File.Exists(fullNetworkName))
                net.Load(fullNetworkName);//on start load the network save

            networks.Add(net);
        }
    }

    public void StartCycle()
    {
        if (bots.Count > 0)
            SortAndMutateNetworks();

        // Spawn bots
        Vector3 startVelocity = Random.insideUnitCircle * maxStartHorizontalSpeed;
        startVelocity.z = startVelocity.y;
        startVelocity.y = startVerticalSpeed;

        if (bots.Count == 0)
        {
            for (int i = 0; i < populationSize; i++)
                bots.Add(Instantiate(botPrefab, transform.position, Quaternion.identity));

        }

        for (int i = 0; i < bots.Count; i++)
        {
            TrainingBotBase bot = bots[i];

            bot.network = networks[i];//deploys network to each learner
            bot.target = checkpoints.Count > 0 ? checkpoints[0].transform : null;
            bot.checkpoints = checkpoints;
            bot.checkpointRadius = checkpointRadius;
            bot.movement.velocity = startVelocity;
            bot.movement.state = 0;
            bot.localPlayer = localPlayer;
            bot.trainer = this;

            bot.Reset();
        }

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

        // Setup local player to optionally race the bots
        if (localPlayer && resetLocalPlayerOnCycle)
        {
            localPlayer.transform.position = transform.position;
            localPlayer.velocity = startVelocity;
        }

        onCycle?.Invoke();

        lastRunTime = Time.time;
    }

    private void OnDisable()
    {
        if (networks.Count == bots.Count && bots.Count > 0)
            networks[populationSize - 1].Save(fullNetworkNameBackup);
    }

    public void SortAndMutateNetworks()
    {
        for (int i = 0; i < populationSize; i++)
            bots[i].network.fitness = bots[i].GetFitness();

        networks.Sort();

        Debug.Log("Top fitness " + (networks[populationSize - 1].fitness).ToString());

        if (saveResults)
        {
            networks[populationSize - 1].Save(fullNetworkName);
        }

        for (int i = 0; i < populationSize / 2; i++)
        {
            networks[i] = networks[i + populationSize / 2].copy(new NeuralNetwork(botPrefab.layers));
            networks[i].Mutate((int)(1/mutationChance), mutationStrength);
        }
    }
}