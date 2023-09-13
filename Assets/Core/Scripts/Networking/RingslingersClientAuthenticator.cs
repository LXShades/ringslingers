using Mirror;
using Mirror.Authenticators;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

public class RingslingersClientAuthenticator : NetworkAuthenticator
{
    private int maxRejectionQueueLength = 10;
    private Queue<NetworkConnection> rejectionQueue = new Queue<NetworkConnection>();

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

    private string lastModLoadMessage = null;

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
            rejectionQueue.Enqueue(source);
            StartCoroutine(RejectClientRoutine());
            //ServerReject(source);
        }
    }

    private void ClientOnServerResponseMessage(ServerResponseMessage response)
    {
        if (!string.IsNullOrEmpty(response.error))
        {
            // Client was rejected
            Debug.LogError($"Rejected by server: {response.error}");

            string error = $"Cannot join this server:\n{response.error}";
            if (lastModLoadMessage != null)
                error += $"\n{lastModLoadMessage}";

            Netplay.singleton.DisconnectSelfWithMessage(error, true);
            ClientReject();
        }
        else
        {
            Debug.Log($"Accepted by server");
            ClientAccept();
        }
    }

    // reject clients with a delay so we should have enough time to tell them goodbye
    private IEnumerator RejectClientRoutine()
    {
        while (rejectionQueue.Count > 0)
        {
            yield return new WaitForSeconds(1);
            ServerReject(rejectionQueue.Dequeue());
        }
    }

    private void ClientOnServerModsMessage(ServerModsMessage mods)
    {
        if (mods.loadedMods.Length > 0)
        {
            Debug.Log($"Server has mods enabled, adding them now");

            ModManager.TrySyncMods(mods.loadedMods, (bool wasSuccessful, string message) =>
            {
                if (wasSuccessful)
                    Debug.Log($"Mod load SUCCEEDED: {message}");
                else
                    Debug.LogError($"Mod load FAILED: {message}");

                lastModLoadMessage = message;

                NetworkClient.connection.Send(new ClientModStatusMessage() { hasAllRequiredMods = wasSuccessful });
            });
        }
        else
        {
            lastModLoadMessage = null;
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
