using Mirror;
using Mirror.Authenticators;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

public class RingslingersClientAuthenticator : NetworkAuthenticator
{
    public struct ServerModsMessage : NetworkMessage
    {
        public RingslingersMod[] loadedMods;
    }

    public struct ServerResponseMessage : NetworkMessage
    {
        public string error;
    }

    public struct ClientJoinMessage : NetworkMessage
    {
        public string gameVersion;
    }

    public struct ClientModStatusMessage : NetworkMessage
    {
        public bool hasAllRequiredMods;
    }

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<ClientJoinMessage>(ServerOnClientJoinMessage, false);
        NetworkServer.RegisterHandler<ClientModStatusMessage>(ServerOnClientModStatusMessage, false);
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<ClientJoinMessage>();
        NetworkServer.UnregisterHandler<ClientModStatusMessage>();
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<ServerResponseMessage>(ClientOnServerResponseMessage, false);
        NetworkClient.RegisterHandler<ServerModsMessage>(ClientOnServerModsMessage, false);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<ServerResponseMessage>();
        NetworkClient.UnregisterHandler<ServerModsMessage>();
    }

    private void ServerOnClientJoinMessage(NetworkConnection source, ClientJoinMessage joinRequest)
    {
        // TODO: if server is still loading mods, wait until they're loaded before telling the client which mods

        if (joinRequest.gameVersion == Application.version)
        {
            // client has the correct version of the game, now do they have the correct mods?
            source.Send(new ServerModsMessage()
            {
                loadedMods = ModManager.loadedMods.ToArray()
            });
        }
        else
        {
            source.Send(new ServerResponseMessage() { error = $"Game version is incorrect. Server={Application.version} Local={joinRequest.gameVersion}" });
            ServerReject(source);
        }
    }

    private void ServerOnClientModStatusMessage(NetworkConnection source, ClientModStatusMessage modStatus)
    {
        if (modStatus.hasAllRequiredMods)
        {
            source.Send(new ServerResponseMessage() { });
            ServerAccept(source);
        }
        else
        {
            source.Send(new ServerResponseMessage() { error = $"You don't have the necessary mods" });
            ServerReject(source);
        }
    }

    private void ClientOnServerResponseMessage(ServerResponseMessage response)
    {
        if (!string.IsNullOrEmpty(response.error))
        {
            Debug.LogError($"Rejected by server: {response.error}");
            ClientReject();
        }
        else
        {
            Debug.Log($"Accepted by server");
            ClientAccept();
        }
    }

    private void ClientOnServerModsMessage(ServerModsMessage mods)
    {
        if (mods.loadedMods.Length > 0)
        {
            Debug.Log($"Server has mods enabled, adding them now");

            ModManager.LoadMods(mods.loadedMods, (bool wasSuccessful, string message) =>
            {
                Debug.Log($"Mod load success: {wasSuccessful} message: {message}");

                NetworkClient.connection.Send(new ClientModStatusMessage() { hasAllRequiredMods = wasSuccessful });
            });
        }
        else
        {
            NetworkClient.connection.Send(new ClientModStatusMessage() { hasAllRequiredMods = true });
        }
    }

    public override void OnClientAuthenticate()
    {
        NetworkClient.connection.Send(new ClientJoinMessage()
        {
            gameVersion = Application.version
        });
    }

    public override void OnServerAuthenticate(NetworkConnection conn)
    {
    }
}
