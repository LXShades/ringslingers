using Mirror;
using UnityEngine;

public class Player : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId;

    private Mirror.HistoryList<int> testList = new Mirror.HistoryList<int>();

    private Character character => playerId != -1 ? Netplay.singleton.players[playerId] : null;

    public override void OnStartServer()
    {
        NetworkConnection connection = netIdentity.connectionToClient;

        // spawn the player
        Character newPlayer = Netplay.singleton.AddPlayer();
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
        CmdRequestCharacter(Netplay.singleton.localPlayerIndendedCharacter);
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
        MessageFeed.Post($"<{character?.playerName}> <noparse>{message}</noparse>", true);
    }

    [Command]
    private void CmdTryRename(string newName)
    {
        string oldName = character.playerName;

        character?.Rename(newName);

        if (oldName != character.playerName)
            MessageFeed.Post($"{oldName} was renamed to <player>{character.playerName}</player>");
    }

    [Command]
    public void CmdRequestCharacter(int characterIndex)
    {
        Character player = Netplay.singleton.ChangePlayerCharacter(playerId, characterIndex);

        if (player != null)
        {
            playerId = player.playerId;
            player.netIdentity.AssignClientAuthority(netIdentity.connectionToClient);
        }
    }

    [Command]
    public void CmdRequestColor(Color32 colour)
    {
        character?.TryChangeColour(colour);
    }
}
