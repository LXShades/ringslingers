using Mirror;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameState except properly networked this time!
/// Override based on the game mode e.g. NetGameStateMatch and NetGameStateFreePlay
/// </summary>
public class NetGameState : NetworkBehaviour
{
    // singleton pattern abuse!?
    public static NetGameState singleton { get; private set; }

    public virtual bool HasRoundFinished { get; set; } = false;

    public virtual bool IsWinScreen { get; set; } = false;

    /// <summary>
    /// Sets the gamestate by spawning one on the server
    /// </summary>
    [Server]
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

    private void Awake()
    {
        if (singleton != null)
        {
            Destroy(gameObject);
            Log.WriteWarning("There is already a NetGameState running");
            return;
        }

        singleton = this;
        OnAwake();
    }

    private void Start()
    {
        OnStart();
    }

    private void Update()
    {
        OnUpdate();
    }

    public virtual void OnAwake() { }

    public virtual void OnUpdate() { }

    public virtual void OnStart() { }

    /// <summary>
    /// Called on server and client in this game mode when a player is created
    /// </summary>
    public virtual void OnPlayerStart(PlayerController player) { }

    public virtual List<PlayerController> GetWinners() { return new List<PlayerController>(); }
}
