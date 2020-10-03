using Mirror;
using UnityEngine;

/// <summary>
/// Netplay is a manager that holds information on players and synced objects, and handles synchronisation
/// </summary>
public class Netplay : MonoBehaviour
{
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
    private ulong[] playerClientIds = new ulong[16];

    /// <summary>
    /// Player objects by ID. Will contains null gaps
    /// </summary>
    public Player[] players = new Player[16];

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

    private void Awake()
    {
    }

    private bool InitNet()
    {
        if (net)
            return true;

        net = NetMan.singleton;

        if (net == null)
        {
            Debug.LogWarning("No network manager found");
            return false;
        }

        // Register network callbacks
        net.onClientConnect += OnClientConnected;
        net.onClientDisconnect += OnClientDisconnected;

        net.onServerConnect += OnServerConnected;
        net.onServerDisconnect += OnServerDisconnected;

        net.onServerAddPlayer += OnNewPlayer;

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
    public int GetPlayerIdFromClient(ulong clientId)
    {
        int index = System.Array.IndexOf(playerClientIds, clientId);
        return index;
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
            Debug.LogWarning("Cannot connect: Net system could not be initialized");
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
            Debug.LogWarning("Cannot create server: Net system could not be initialized");
        }
    }

    private void OnClientConnected(NetworkConnection connection)
    {
        Debug.Log("Connection successful");

        connectionStatus = ConnectionStatus.Ready;
    }

    private void OnClientDisconnected(NetworkConnection connection)
    {
        Debug.Log("Disconnected from server");

        net.StopClient();
        connectionStatus = ConnectionStatus.Disconnected;
    }

    private void OnServerConnected(NetworkConnection connection)
    {
        Debug.Log("A client has connected!");
    }

    private void OnServerDisconnected(NetworkConnection connection)
    {
        Debug.Log("A client has disconnected");
    }

    private void OnNewPlayer(NetworkConnection connection)
    {
        if (connection.identity)
        {
            // spawn the player
            Player newPlayer = AddPlayer(-1);
            newPlayer.GetComponent<NetworkIdentity>().AssignClientAuthority(connection);

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
            Debug.LogWarning("Can't add new player - too many!");
            return null;
        }

        // Spawn the player
        Player player = World.Spawn(GameManager.singleton.playerPrefab).GetComponent<Player>();

        player.playerId = id;
        players[id] = player;

        player.Rename($"Fred");

        player.Respawn();

        Debug.Log($"{player.playerName} ({player.playerId}) has entered the game");

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
    private int numReceivedTicks = 0;
    private int numSentTicks = 0;
    private int numReceivedBytes = 0;

    void UpdateNetStat()
    {
        netStatFrameNum++;

        // Update netstat
        if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
        {
            float averageTicksPerFrame = 0;
            int maxTicksPerFrame = System.Int32.MinValue, minTicksPerFrame = System.Int32.MaxValue;
            int numFramesWhereTicksWereReceived = 0;

            for (int i = 0; i < Mathf.Min(netStatFrameNum, numTicksPerFrame.Length); i++)
            {
                averageTicksPerFrame += numTicksPerFrame[i];
                if (numTicksPerFrame[i] > 0)
                {
                    numFramesWhereTicksWereReceived++;
                    maxTicksPerFrame = Mathf.Max(maxTicksPerFrame, numTicksPerFrame[i]);
                    minTicksPerFrame = Mathf.Min(minTicksPerFrame, numTicksPerFrame[i]);
                }
            }
            averageTicksPerFrame /= Mathf.Max(numFramesWhereTicksWereReceived, 1);

            netStat = $"Bytes recv: {numReceivedBytes}\nTicks recv: {numReceivedTicks}\nTicks sent: {numSentTicks}\nAvg ticks per frame: {averageTicksPerFrame} (max {maxTicksPerFrame} min {minTicksPerFrame}";
            numSentTicks = 0;
            numReceivedTicks = 0;
            numReceivedBytes = 0;
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
