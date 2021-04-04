﻿using Mirror;
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
        MessageFeed.Post($"<{player?.playerName}> <noparse>{message}</noparse>", true);
    }

    [Command]
    private void CmdTryRename(string newName)
    {
        string oldName = player.playerName;

        player?.Rename(newName);

        if (oldName != player.playerName)
            MessageFeed.Post($"{oldName} was renamed to <player>{player.playerName}</player>");
    }

    [Command]
    public void CmdRequestCharacter(int characterIndex)
    {
        Player player = Netplay.singleton.ChangePlayerCharacter(playerId, characterIndex);

        if (player != null)
        {
            playerId = player.playerId;
            player.netIdentity.AssignClientAuthority(netIdentity.connectionToClient);
        }
    }

    [Command]
    public void CmdRequestColor(Color32 colour)
    {
        player?.TryChangeColour(colour);
    }
}
