using Mirror;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameState except properly networked this time!
/// Override based on the game mode e.g. NetGameStateMatch and NetGameStateFreePlay
/// </summary>
public class MatchState : NetworkBehaviour
{
    // singleton pattern abuse!?
    public static MatchState singleton { get; private set; }

    public bool IsWinScreen => _isWinScreen;

    public int intermissionTime = 15;

    public float timeTilRestart { get; private set; }

    List<MatchStateComponent> components = new List<MatchStateComponent>();

    [SyncVar]
    private bool _isWinScreen = false;

    /// <summary>
    /// Sets the gamestate by spawning one on the server
    /// </summary>
    //[Server]
    public static void SetNetGameState(GameObject newGameStatePrefab)
    {
        if (singleton != null)
        {
            Destroy(singleton.gameObject);
            singleton = null;
        }

        GameObject newState = Instantiate(newGameStatePrefab);
        NetworkServer.Spawn(newState);
    }

    /// <summary>
    /// Returns a gamestate comopnent, if it exists in the current game state
    /// </summary>
    public static bool Get<TComponent>(out TComponent netGameStateComponent) where TComponent : Component
    {
        netGameStateComponent = null;
        return singleton != null ? singleton.TryGetComponent<TComponent>(out netGameStateComponent) : false;
    }

    private void Awake()
    {
        if (singleton != null)
        {
            Destroy(gameObject);
            Log.WriteWarning("There is already a NetGameState running");
            return;
        }

        foreach (MatchStateComponent matchComponent in GetComponents<MatchStateComponent>())
        {
            components.Add(matchComponent);
            matchComponent.OnAwake();
        }

        timeTilRestart = intermissionTime;

        singleton = this;
    }

    private void Start()
    {
        foreach (MatchStateComponent component in components)
            component.OnStart();
    }

    private void Update()
    {
        foreach (MatchStateComponent component in components)
            component.OnUpdate();

        if (IsWinScreen && isServer)
        {
            if ((int)timeTilRestart - Time.deltaTime != (int)timeTilRestart)
                RpcTimeTilRestart(timeTilRestart);

            timeTilRestart -= Time.deltaTime;

            if (timeTilRestart <= 0f)
            {
                _isWinScreen = false;

                GameState.Get<GameState_Map>().ServerNextMap();
            }
        }
    }

    public string GetWinners()
    {
        string winners = "";
        foreach (MatchStateComponent component in components)
        {
            string winner = component.GetWinners();
            if (!string.IsNullOrEmpty(winner))
            {
                if (winners.Length == 0)
                    winners += winner;
                else
                    winners += $" and {winner}";
            }
        }

        return winners;
    }

    /// <summary>
    /// It's ok to spam this function
    /// </summary>
    [Server]
    public void ServerEndRound()
    {
        _isWinScreen = true;
    }

    [Server]
    public void ServerSkipWinScreen()
    {
        if (IsWinScreen)
            timeTilRestart = 0.001f;
    }

    [ClientRpc(channel = Channels.Unreliable)]
    private void RpcTimeTilRestart(float timeRemaining)
    {
        if (!NetworkServer.active)
            timeTilRestart = timeRemaining;
    }
}
