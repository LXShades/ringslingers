using Mirror;

public class MatchStateComponent : NetworkBehaviour
{
    public virtual void OnAwake() { }

    public virtual void OnUpdate() { }

    public virtual void OnStart() { }

    /// <summary>
    /// Called on server and client in this game mode when a player is created
    /// </summary>
    public virtual void OnPlayerStart(PlayerController player) { }

    /// <summary>
    /// Returns the names of the winning party(s)
    /// </summary>
    public virtual string GetWinners() => "";

}
