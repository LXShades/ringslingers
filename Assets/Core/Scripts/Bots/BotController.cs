using Mirror;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class BotController : MonoBehaviour
{
    public LinearPath path;
    public float pathTargetAcceptanceRange = 2f;

    public int followPlayerId;

    private Character character;

    private CharacterInput lastInput;

    public List<IBotTask> activeTasks = new List<IBotTask>();

    public string airNeuralNetworkString;
    public NeuralNetwork airNeuralNetwork { get; private set; }
    public string groundRunNeuralNetworkString;
    public NeuralNetwork groundRunNeuralNetwork { get; private set; }

    bool isInitialised = false;

    private void Awake()
    {
        airNeuralNetwork = new NeuralNetwork(new int[] { 6, 4, 2 });
        airNeuralNetwork.LoadAsString(airNeuralNetworkString);

        groundRunNeuralNetwork = new NeuralNetwork(new int[] { 6, 3, 2 });
        groundRunNeuralNetwork.LoadAsString(groundRunNeuralNetworkString);

        if (activeTasks.Count == 0)
            GetOrActivateTask<BT_CollectAndShoot>();
    }

    private void Update()
    {
        // draw task gizmos
        foreach (IBotTask task in activeTasks)
        {
            if (task is IBotDebugDraws withGizmos)
                withGizmos.DrawDebugs();
        }
    }

    public void OnInputTick()
    {
        if (character == null)
        {
            Player myPlayer = GetComponent<Player>();

            if (myPlayer)
            {
                character = Netplay.singleton.characters[myPlayer.playerId];

                if (character)
                    character.playerName = gameObject.name;
            }
        }

        if (NetworkServer.active && character)
        {
            // state might need initializing late
            if (!isInitialised)
            {
                isInitialised = true;
                foreach (var state in activeTasks)
                    state.Init(MakeBotTaskParams());
            }

            BotTaskParams taskParams = MakeBotTaskParams();
            lastInput = default;
            for (int i = 0; i < activeTasks.Count; i++)
                activeTasks[i].Update(in taskParams, ref lastInput);

            float inputVisualiserMaxLength = 2f;
            DebugDraw.DrawArrow(character.transform.position, character.transform.position + lastInput.worldMovementDirection.normalized * inputVisualiserMaxLength, Color.red);
            DebugDraw.DrawArrow(character.transform.position, character.transform.position + lastInput.worldMovementDirection * inputVisualiserMaxLength, Color.blue);

            lastInput = lastInput.WithDeltas(character.entity.latestInput);
            GameTicker.singleton.OnRecvBotInput(character.playerId, lastInput);
        }
    }

    public TTask GetOrActivateTask<TTask>() where TTask : IBotTask, new()
    {
        TTask task = (TTask)activeTasks.Find(a => a.GetType() == typeof(TTask));

        if (task == null)
        {
            activeTasks.Add(task = new TTask());

            if (isInitialised)
                task.Init(MakeBotTaskParams());
        }

        return task;
    }

    public TTask GetTask<TTask>() where TTask : IBotTask, new()
    {
        TTask task = (TTask)activeTasks.Find(a => a.GetType() == typeof(TTask));
        return task;
    }

    public TTask ActivateTask<TTask>(TTask task) where TTask : IBotTask
    {
        DeactivateState<TTask>();
        activeTasks.Add(task);

        if (isInitialised)
            task.Init(MakeBotTaskParams());

        return task;
    }

    private BotTaskParams MakeBotTaskParams()
    {
        return new BotTaskParams() { character = character, controller = this, deltaTime = 1f / GameTicker.singleton.fixedInputRate, movement = character?.movement, characterObject = character?.gameObject, lastInput = lastInput };
    }

    public void DeactivateState<TState>() where TState : IBotTask
    {
        activeTasks.RemoveAll(a => a.GetType() == typeof(TState));
    }

    public void ClearStates()
    {
        activeTasks.Clear();
    }
}
