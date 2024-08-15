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

    public List<IBotTask> activeStates = new List<IBotTask>();

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

        if (activeStates.Count == 0)
            GetOrActivateState<BT_CollectAndShoot>();
    }

    public void OnInputTick()
    {
        if (character == null)
        {
            Player myPlayer = GetComponent<Player>();

            if (myPlayer)
            {
                character = Netplay.singleton.players[myPlayer.playerId];

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
                foreach (var state in activeStates)
                    state.Init(MakeBotTaskParams());
            }

            BotTaskParams taskParams = MakeBotTaskParams();
            for (int i = 0; i < activeStates.Count; i++)
                activeStates[i].Update(in taskParams, ref lastInput);

            lastInput = lastInput.WithDeltas(character.entity.latestInput);
            GameTicker.singleton.OnRecvBotInput(character.playerId, lastInput);
        }
    }

    public TState GetOrActivateState<TState>() where TState : IBotTask, new()
    {
        TState state = (TState)activeStates.Find(a => a.GetType() == typeof(TState));

        if (state == null)
        {
            activeStates.Add(state = new TState());

            if (isInitialised)
                state.Init(MakeBotTaskParams());
        }

        return state;
    }

    private BotTaskParams MakeBotTaskParams()
    {
        return new BotTaskParams() { character = character, controller = this, deltaTime = 1f / GameTicker.singleton.fixedInputRate, movement = character?.movement, characterObject = character?.gameObject };
    }

    public void DeactivateState<TState>() where TState : IBotTask
    {
        activeStates.RemoveAll(a => a.GetType() == typeof(TState));
    }

    public void ClearStates()
    {
        activeStates.Clear();
    }
}
