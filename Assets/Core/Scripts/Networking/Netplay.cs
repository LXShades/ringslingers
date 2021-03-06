using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Netplay is a manager that holds information on players and synced objects, and handles synchronisation
/// </summary>
public class Netplay : MonoBehaviour
{
    public struct PingMessage : NetworkMessage
    {
        public ushort time;
        public bool isReliable;
    }

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

    [Header("Connection")]
    public ConnectionStatus connectionStatus = ConnectionStatus.Offline;

    [Range(0.5f, 20f)]
    public float pingsPerSecond = 2f;

    [Header("Tickrate")]
    public float playerTickrate = 10f;

    public bool isPlayerTick => (int)(Time.unscaledTime * playerTickrate) != (int)((Time.unscaledTime - Time.unscaledDeltaTime) * playerTickrate);

    [Header("Players")]
    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId = -1;

    public PlayerClient localClient => NetworkClient.connection.identity.GetComponent<PlayerClient>();

    public string localPlayerIntendedName { get; set; }
    public Color localPlayerIntendedColour { get; set; }

    public Player localPlayer => localPlayerId != -1 ? players[localPlayerId] : null;

    /// <summary>
    /// Player objects by ID. May contain null gaps
    /// </summary>
    public readonly List<Player> players = new List<Player>();

    /// <summary>
    /// Whether this is the server player
    /// </summary>
    public bool isServer => net.mode != Mirror.NetworkManagerMode.ClientOnly;

    /// <summary>
    /// Whether this is not a server or host player
    /// </summary>
    public bool isClient => net.mode == Mirror.NetworkManagerMode.ClientOnly;

    private NetMan net;

    public float unreliablePing { get; private set; }
    public float reliablePing { get; private set; }

    private uint msTime; // time in ms since InitNet
    private uint lastPingMsTime;

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

        NetworkDiagnostics.InMessageEvent += NetworkDiagnostics_InMessageEvent;
        NetworkDiagnostics.OutMessageEvent += NetworkDiagnostics_OutMessageEvent;

        NetworkClient.RegisterHandler<PingMessage>(OnClientPingMessageReceived);
        NetworkServer.RegisterHandler<PingMessage>(OnServerPingMessageReceived);

        SceneManager.activeSceneChanged += OnSceneChanged;
        net.onServerStarted += StartMatch;

        msTime = 0;

        return true;
    }

    void Update()
    {
        // Do debug stuff
        UpdateNetStat();

        // Update SyncActions
        SyncActionChain.Tick();

        // Update ping
        msTime += (uint)Mathf.RoundToInt(Time.deltaTime * 1000);
        if (NetworkClient.isConnected && msTime - lastPingMsTime > 1000f / pingsPerSecond)
        {
            SendPings();
        }
    }

    public void ServerNextMap()
    {
        if (!NetworkServer.active)
        {
            Log.WriteWarning("Only the server can change the map");
            return;
        }

        LevelDatabase db = GameManager.singleton.levelDatabase;

        if (db.levels.Length == 0)
            return;

        int currentLevelIndex = -1;

        for (int i = 0; i < db.levels.Length; i++)
        {
            if (db.levels[i].path.ToLower() == SceneManager.GetActiveScene().path.ToLower())
                currentLevelIndex = i;
        }

        currentLevelIndex = (currentLevelIndex + 1) % db.levels.Length;
        NetMan.singleton.ServerChangeScene(db.levels[currentLevelIndex].path, true);
    }

    #region Game
    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        StartMatch();
    }

    /// <summary>
    /// Called when the level starts or a server starts
    /// </summary>
    private void StartMatch()
    {
        if (NetworkServer.active)
        {
            LevelConfigurationComponent config = FindObjectOfType<LevelConfigurationComponent>();

            if (config != null)
                MatchState.SetNetGameState(config.configuration.defaultGameModePrefab);
            else
                Debug.LogError("We can't play this map. There's no game mode setup!");
        }
    }
    #endregion

    #region Ping
    private void SendPings()
    {
        if (NetworkClient.isConnected)
        {
            NetworkClient.Send(new PingMessage()
            {
                time = (ushort)msTime,
                isReliable = false
            }, Channels.DefaultUnreliable);
            NetworkClient.Send(new PingMessage()
            {
                time = (ushort)msTime,
                isReliable = true
            }, Channels.DefaultReliable);
        }
        lastPingMsTime = msTime;
    }

    private void OnClientPingMessageReceived(PingMessage message)
    {
        // resolve the received time
        uint receivedTime = message.time | (msTime & 0xFFFF0000);

        if (receivedTime > msTime)
        {
            // we've wrapped around since this message!
            // see e.g. (where last two digits are the only digits we've actually received)
            // received              = 3 99
            // new                   = 4 02
            // likely real received  = 3 99 (not 4 99 like we assembled above)
            receivedTime -= 0x10000;
        }

        if (message.isReliable)
            reliablePing = (msTime - receivedTime) / 1000f;
        else
            unreliablePing = (msTime - receivedTime) / 1000f;
    }

    private void OnServerPingMessageReceived(NetworkConnection source, PingMessage message)
    {
        source.Send(message, message.isReliable ? Channels.DefaultReliable : Channels.DefaultUnreliable); // pong!
    }
    #endregion

    #region Players
    /// <summary>
    /// Gets the player ID from a client ID. Returns -1 if not found
    /// </summary>
    public int GetPlayerIdFromConnectionId(int connectionId)
    {
        if (NetworkServer.active && NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient connection))
        {
            int? id = connection.identity?.GetComponent<PlayerClient>()?.playerId;

            if (id != null)
                return (int)id;
        }

        return -1;
    }
    #endregion

    #region Connection
    public void ConnectToServer(string ipString)
    {
        if (net || InitNet())
        {
            net.Connect(ipString);
        }
        else
        {
            Log.WriteWarning("Cannot connect: Net system could not be initialized");
        }
    }

    public void HostServer(int port = -1)
    {
        if (net || InitNet())
        {
            net.Host(true, port);

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
        foreach (NetworkIdentity identity in connection.clientOwnedObjects)
        {
            if (identity != null && identity.TryGetComponent(out Player clientPlayer))
            {
                MessageFeed.Post($"<player>{clientPlayer.playerName}</player> has left the game.");
            }
        }

        Log.Write("A client has disconnected");
    }
    #endregion

    #region Players
    public Player AddPlayer()
    {
        if (!NetworkServer.active)
        {
            Debug.Log("Only the server can create players!");
            return null;
        }

        // Spawn the player
        Player player = Spawner.Spawn(GameManager.singleton.playerPrefab).GetComponent<Player>();

        player.Rename($"Player {player.playerId}");
        Log.Write($"{player.playerName} ({player.playerId}) has entered the game");
        MessageFeed.Post($"<player>{player.playerName}</player> has joined the game!");

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

    /// <summary>
    /// Registers a player into the player list and returns its ID
    /// </summary>
    public void RegisterPlayer(Player player, int id = -1)
    {
        if (id == -1)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null || players[i] == player)
                {
                    players[i] = player;
                    player.playerId = i;
                    return;
                }
            }

            // no space found
            players.Add(player);
            player.playerId = players.Count - 1;
        }
        else
        {
            // we might be a client registering awareness of this player
            while (players.Count <= id)
                players.Add(null);
            
            players[id] = player;
            player.playerId = id;
        }
    }

    public Player FindPlayer(string name)
    {
        foreach (Player player in players)
        {
            if (player.name == name)
                return player;
        }
        return null;
    }
    #endregion

    #region Debugging
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
        // Update netstat
        if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
        {
            netStat = $"Send/Recv: {numSentBytes/1024f:0.0}KB/{numReceivedBytes / 1024f:0.0}KB\nSend/Recv: {numSentBytes / 128f:0.0}Kbits/{numReceivedBytes / 128f:0.0}Kbits";
            numReceivedBytes = 0;
            numSentBytes = 0;

            System.Array.Clear(numTicksPerFrame, 0, numTicksPerFrame.Length);
        }
    }
    #endregion
}
