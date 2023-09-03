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

    public FlowControlSettings defaultFlowControlSettings = FlowControlSettings.Default;

    [Range(0.5f, 20f)]
    public float pingsPerSecond = 2f;

    [Header("Tickrate")]
    public float playerTickrate = 10f;

    public bool isPlayerTick => (int)(Time.unscaledTime * playerTickrate) != (int)((Time.unscaledTime - Time.unscaledDeltaTime) * playerTickrate);

    [Header("Players")]
    [SerializeField] private int _localPlayerId = -1;
    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId
    {
        get => _localPlayerId;
        set
        {
            _localPlayerId = value;
            Spawner.SetLocalPlayerId((byte)value);
        }
    }

    public Player localClient => NetworkClient.connection?.identity != null ? NetworkClient.connection.identity.GetComponent<Player>() : null;

    public Character localPlayer => localPlayerId != -1 ? players[localPlayerId] : null;

    /// <summary>
    /// Player objects by ID. May contain null gaps
    /// </summary>
    public readonly List<Character> players = new List<Character>();

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

        GamePreferences.onPreferencesChanged += ApplyNetPreferences;
        ApplyNetPreferences(); // update net settings now

        msTime = 0;

        return true;
    }

    void Update()
    {
        // Update player scores and crowns
        RefreshPlayerScores();

        // Do debug stuff
        UpdateNetStat();

        // Update ping
        msTime += (uint)Mathf.RoundToInt(Time.deltaTime * 1000);
        if (NetworkClient.isConnected && msTime - lastPingMsTime > 1000f / pingsPerSecond)
        {
            SendPings();
        }
    }

    private void RefreshPlayerScores()
    {
        // Update player crowns, because scores etc
        int bestPlayerScore = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] && players[i].score > bestPlayerScore)
                bestPlayerScore = players[i].score;
        }
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i])
                players[i].isFirstPlace = (players[i].score == bestPlayerScore);
        }
    }

    public void ServerLoadLevel(MapConfiguration level)
    {
        GameManager.singleton.activeLevel = level;
        NetMan.singleton.ServerChangeScene(level.path, true);
    }

    public void ServerNextMap()
    {
        if (!NetworkServer.active)
        {
            Log.WriteWarning("Only the server can change the map");
            return;
        }

        List<MapConfiguration> levels = RingslingersContent.loaded.maps;

        if (levels == null || levels.Count == 0)
        {
            Log.WriteError("Cannot load levels database: list is empty or null");
            return;
        }

        if (levels.Count == 1)
        {
            // reload the current map, it's all we have
            ServerLoadLevel(levels[0]);
        }

        int currentLevelIndex = Mathf.Max(levels.IndexOf(GameManager.singleton.activeLevel), 0);
        int nextLevelIndex = -1;

        // Find the next VALID scene
        for (int i = (currentLevelIndex + 1) % levels.Count; i != currentLevelIndex; i = (i + 1) % levels.Count)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(levels[i].path);

            if (buildIndex != -1 && levels[i].includeInRotation)
            {
                nextLevelIndex = i;
                break;
            }
        }

        if (nextLevelIndex != -1)
            ServerLoadLevel(levels[nextLevelIndex]);
    }

    #region Game
    private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
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
            if (GameManager.singleton.activeLevel != null)
                MatchState.SetNetGameState(GameManager.singleton.activeLevel.defaultGameModePrefab);
            else
                Debug.LogError("We can't play this map properly, for some reason activeLevel is null so we can't find the game mode!");
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
            }, Channels.Unreliable);
            NetworkClient.Send(new PingMessage()
            {
                time = (ushort)msTime,
                isReliable = true
            }, Channels.Reliable);
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
        source.Send(message, message.isReliable ? Channels.Reliable : Channels.Unreliable); // pong!
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
            int? id = connection.identity?.GetComponent<Player>()?.playerId;

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

    public void HostServer(MapConfiguration level)
    {
        if (level != null)
            ServerLoadLevel(level);

        if (net || InitNet())
        {
            net.Host(true);

            connectionStatus = ConnectionStatus.Ready;
            ApplyNetPreferences();
        }
        else
        {
            Log.WriteWarning("Cannot create server: Net system could not be initialized");
        }
    }

    private void OnClientConnected(NetworkConnection connection)
    {
        Log.Write("Connection successful");

        ApplyNetPreferences();
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

        ApplyNetPreferences();
    }

    private void OnServerDisconnected(NetworkConnection connection)
    {
        foreach (NetworkIdentity identity in connection.clientOwnedObjects)
        {
            if (identity != null && identity.TryGetComponent(out Character clientPlayer))
            {
                MessageFeed.Post($"<player>{clientPlayer.playerName}</player> has left the game.");
            }
        }

        Log.Write("A client has disconnected");
    }
    #endregion

    #region Characters
    public Character AddCharacter(int characterIndex, Player owner)
    {
        if (!NetworkServer.active)
        {
            Debug.Log("Only the server can create players!");
            return null;
        }

        // Spawn the player
        Character character = Spawner.Spawn(RingslingersContent.loaded.characters[characterIndex].prefab).GetComponent<Character>();

        character.serverOwningPlayer = owner;
        character.characterIndex = characterIndex;
        character.Rename($"Player {character.playerId}");
        Log.Write($"{character.playerName} ({character.playerId}) has entered the game");
        MessageFeed.Post($"<player>{character.playerName}</player> has joined the game!");

        return character;
    }

    public Character ChangePlayerCharacter(int playerId, int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= RingslingersContent.loaded.characters.Count)
        {
            Debug.Log($"Cannot change to character {characterIndex}: character does not exist");
            return null;
        }

        Debug.Assert(playerId >= 0 && playerId < players.Count);

        string playerName = $"Player {playerId}";
        if (players[playerId] != null)
        {
            playerName = players[playerId].playerName;
            Spawner.Despawn(players[playerId].gameObject);
            players[playerId] = null;
        }

        Character player = Spawner.Spawn(RingslingersContent.loaded.characters[characterIndex].prefab).GetComponent<Character>();

        player.characterIndex = characterIndex;
        player.Rename(playerName);

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
    public void RegisterPlayer(Character player, int id = -1)
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

    public Character FindPlayer(string name)
    {
        foreach (Character player in players)
        {
            if (player.name == name)
                return player;
        }
        return null;
    }
    #endregion

    #region Net Configuration
    private void ApplyNetPreferences()
    {
        // NET FLOW CONTROL
        // disabled because it was unreliable and the ticker refactor smoothes things out in a new more reliable way

        /*FlowControlSettings flowSettings = defaultFlowControlSettings;

        if (NetMan.singleton.mode == NetworkManagerMode.ClientOnly)
        {
            flowSettings.minDelay = GamePreferences.minClientDelayMs * 0.001f;
            flowSettings.maxDelay = GamePreferences.maxClientDelayMs * 0.001f;
        }
        else
        {
            flowSettings.minDelay = GamePreferences.minServerDelayMs * 0.001f;
            flowSettings.maxDelay = GamePreferences.maxServerDelayMs * 0.001f;
        }

        if (NetworkClient.active && !NetworkServer.active /* don't throttle host self-connection *//*)
        {
            NetworkClient.unbatcher.enableFlowControl = GamePreferences.isNetFlowControlEnabled;
            NetworkClient.unbatcher.flowControlSettings = flowSettings;
        }

        if (NetworkServer.active)
        {
            foreach (KeyValuePair<int, NetworkConnectionToClient> kp in NetworkServer.connections)
            {
                NetworkConnectionToClient conn = kp.Value;
                if (conn != NetworkServer.localConnection)
                {
                    conn.unbatcher.enableFlowControl = GamePreferences.isNetFlowControlEnabled;
                    conn.unbatcher.flowControlSettings = flowSettings;
                }
            }
        }*/
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
            netStat = $"Send/Recv: {numSentBytes / 1024f:0.0}KB/{numReceivedBytes / 1024f:0.0}KB\nSend/Recv: {numSentBytes / 128f:0.0}Kbits/{numReceivedBytes / 128f:0.0}Kbits";
            numReceivedBytes = 0;
            numSentBytes = 0;

            System.Array.Clear(numTicksPerFrame, 0, numTicksPerFrame.Length);
        }
    }
    #endregion
}
