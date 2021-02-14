using Mirror;
using UnityEngine;

public class PlayerClient : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId;

    private Player player => playerId != -1 ? Netplay.singleton.players[playerId] : null;

    public override void OnStartServer()
    {
        NetworkConnection connection = netIdentity.connectionToClient;

        // spawn the player
        Player newPlayer = Netplay.singleton.AddPlayer();
        newPlayer.netIdentity.AssignClientAuthority(connection);

        playerId = newPlayer.playerId;

        base.OnStartServer();
    }

    public override void OnStartLocalPlayer()
    {
        // Request our name upon joining
        base.OnStartLocalPlayer();

        Netplay.singleton.localPlayerId = playerId;
        CmdTryRename(Netplay.singleton.localPlayerIntendedName);
        CmdRequestColor(Netplay.singleton.localPlayerIntendedColour);
    }

    void OnPlayerIdChanged(int oldValue, int newValue)
    {
        playerId = newValue;

        if (isLocalPlayer)
        {
            Netplay.singleton.localPlayerId = newValue;
        }
    }

    [Command]
    public void CmdSendMessage(string message)
    {
        message = message.Replace("</noparse>", "lol"); // plz don't
        MessageFeed.Post($"<<player>{player?.playerName}</player>>: <noparse>{message}</noparse>");
    }

    [Command]
    private void CmdTryRename(string newName)
    {
        player?.Rename(newName);
    }

    [Command]
    public void CmdRequestColor(Color32 colour)
    {
        player?.TryChangeColour(colour);
    }
}
