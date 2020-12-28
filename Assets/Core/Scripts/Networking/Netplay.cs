using Mirror;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Netplay is a manager that holds information on players and synced objects, and handles synchronisation
/// </summary>
public class Netplay : MonoBehaviour
{
    public const int kMaxNumPlayers = 16;

    public static Netplay singleton
    {
        get
        {
            if (_singleton == null)
                _singleton = FindObjectOfType<Netplay>();

            return _singleton;
        }
    }
    private static Netplay _singleton;

    public enum ConnectionStatus
    {
        Offline = 0,
        Ready,
        Connecting,
        Disconnected
    };

    public ConnectionStatus connectionStatus = ConnectionStatus.Offline;

    [Header("Tickrate")]
    public float playerTickrate = 10f;

    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId = -1;

    /// <summary>
    /// Used by the server. Used to defer new player creation
    /// </summary>
    private readonly Dictionary<int, int> playerIdFromConnectionId = new Dictionary<int, int>();

    /// <summary>
    /// Player objects by ID. Will contains null gaps
    /// </summary>
    public Player[] players = new Player[kMaxNumPlayers];

    /// <summary>
    /// Whether this is the server player
    /// </summary>
    public bool isServer => net.mode != Mirror.NetworkManagerMode.ClientOnly;

    /// <summary>
    /// Whether this is not a server or host player
    /// </summary>
    public bool isClient => net.mode == Mirror.NetworkManagerMode.ClientOnly;

    private NetMan net;

    public string netStat
    {
        get; private set;
    }

    private bool InitNet()
    {
        if (net)
            return true;

        net = NetMan.singleton;

        if (net == null)
        {
            Log.WriteWarning("No network manager found");
            return false;
        }

        // Register network callbacks
        net.onClientConnect += OnClientConnected;
        net.onClientDisconnect += OnClientDisconnected;

        net.onServerConnect += OnServerConnected;
        net.onServerDisconnect += OnServerDisconnected;

        net.onServerAddPlayer += OnNewPlayer;

        NetworkDiagnostics.InMessageEvent += NetworkDiagnostics_InMessageEvent;
        NetworkDiagnostics.OutMessageEvent += NetworkDiagnostics_OutMessageEvent;

        return true;
    }

    /// <summary>
    /// Runs a networking tick
    /// </summary>
    public void Tick()
    {
        // Do debug stuff
        UpdateNetStat();

        SyncActionChain.Tick();
    }

    #region Players
    /// <summary>
    /// Gets the player ID from a client ID. Returns -1 if not found
    /// </summary>
    public int GetPlayerIdFromConnectionId(int connectionId)
    {
        if (!NetworkServer.active) // only the server has accurate connection IDs
            return -1;

        if (playerIdFromConnectionId.TryGetValue(connectionId, out int playerId))
            return playerId;

        return -1;
    }
    #endregion

    #region Connection
    public void ConnectToServer(string ipString)
    {
        if (net || InitNet())
        {
            net.StartClient();
        }
        else
        {
            Log.WriteWarning("Cannot connect: Net system could not be initialized");
        }
    }

    public void CreateServer()
    {
        if (net || InitNet())
        {
            // should be Frame.server, serialization/deserialization is still todo
            localPlayerId = 0;

            net.StartHost();

            connectionStatus = ConnectionStatus.Ready;
        }
        else
        {
            Log.WriteWarning("Cannot create server: Net system could not be initialized");
        }
    }

    private void OnClientConnected(NetworkConnection connection)
    {
        Log.Write("Connection successful");

        connectionStatus = ConnectionStatus.Ready;
    }

    private void OnClientDisconnected(NetworkConnection connection)
    {
        Log.Write("Disconnected from server");

        net.StopClient();
        connectionStatus = ConnectionStatus.Disconnected;
    }

    private void OnServerConnected(NetworkConnection connection)
    {
        Log.Write("A client has connected!");
    }

    private void OnServerDisconnected(NetworkConnection connection)
    {
        Log.Write("A client has disconnected");
    }

    private void OnNewPlayer(NetworkConnection connection)
    {
        if (connection.identity)
        {
            // spawn the player
            Player newPlayer = AddPlayer(-1);
            newPlayer.GetComponent<NetworkIdentity>().AssignClientAuthority(connection);

            playerIdFromConnectionId[connection.connectionId] = newPlayer ? newPlayer.playerId : -1;
            connection.identity.GetComponent<PlayerClient>().playerId = newPlayer ? newPlayer.playerId : -1;
        }
    }
    #endregion

    #region Players
    public Player AddPlayer(int id = -1)
    {
        if (id == -1)
        {
            // Find the appropriate ID for this player
            for (id = 0; id < players.Length; id++)
            {
                if (players[id] == null)
                    break;
            }
        }

        if (id == players.Length)
        {
            Log.WriteWarning("Can't add new player - too many!");
            return null;
        }

        // Spawn the player
        Player player = Spawner.Spawn(GameManager.singleton.playerPrefab).GetComponent<Player>();

        player.gameObject.name = $"Player {player.playerId}";
        player.playerId = id;
        players[id] = player;

        player.Rename($"Fred");

        player.Respawn();

        Log.Write($"{player.playerName} ({player.playerId}) has entered the game");

        return player;
    }

    public void RemovePlayer(int id)
    {
        if (players[id] != null)
        {
            Destroy(players[id].gameObject);
            players[id] = null;
        }
    }
    #endregion

    #region Debugging
    private int netStatFrameNum = 0;
    private int[] numTicksPerFrame = new int[500];
    private int numReceivedBytes = 0;
    private int numSentBytes = 0;

    private void NetworkDiagnostics_OutMessageEvent(NetworkDiagnostics.MessageInfo obj)
    {
        numSentBytes += (obj.bytes + 40) * obj.count;
    }

    private void NetworkDiagnostics_InMessageEvent(NetworkDiagnostics.MessageInfo obj)
    {
        numReceivedBytes += (obj.bytes + 40) * obj.count;
    }

    void UpdateNetStat()
    {
        netStatFrameNum++;

        // Update netstat
        if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
        {
            netStat = $"Send/Recv: {numSentBytes/1024f:0.0}KB/{numReceivedBytes / 1024f:0.0}KB\nSend/Recv: {numSentBytes / 128f:0.0}Kbits/{numReceivedBytes / 128f:0.0}Kbits";
            numReceivedBytes = 0;
            numSentBytes = 0;
            netStatFrameNum = 0;

            System.Array.Clear(numTicksPerFrame, 0, numTicksPerFrame.Length);
        }
    }

    public float GetPing()
    {
        return 0f;
    }
    #endregion
}
