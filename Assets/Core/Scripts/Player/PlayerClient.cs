﻿using Mirror;

public class PlayerClient : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId;

    private Player player => playerId != -1 ? Netplay.singleton.players[playerId] : null;

    void OnPlayerIdChanged(int oldValue, int newValue)
    {
        playerId = newValue;

        if (isLocalPlayer)
        {
            Netplay.singleton.localPlayerId = newValue;
        }
    }

    public override void OnStartLocalPlayer()
    {
        // Request our name upon joining
        base.OnStartLocalPlayer();

        CmdTryRename(Netplay.singleton.localPlayerIntendedName);
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
}
