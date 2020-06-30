using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using System.IO;
using UnityEngine.Audio;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Security.Policy;

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
        Ready = 0,
        Connecting = 1,
        Disconnected = 2
    };

    public ConnectionStatus connectionStatus;

    [Header("Synced objects")]
    /// <summary>
    /// Complete list of syncedObjects in the active scene
    /// </summary>
    public List<SyncedObject> syncedObjects = new List<SyncedObject>();

    [Header("Players")]
    /// <summary>
    /// Players by ID. Contains null gaps
    /// </summary>
    public Player[] players = new Player[maxPlayers];

    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId = -1;

    private InputCmds localInputCmds;

    /// <summary>
    /// Prefab used to spawn players
    /// </summary>
    public GameObject playerPrefab;

    public const int maxPlayers = 16;

    [Header("Tick")]
    /// <summary>
    /// The tickrate of the server
    /// </summary>
    public float serverTickRate = 20;

    public float serverDeltaTime
    {
        get
        {
            Debug.Assert(serverTickRate != 0);
            return 1f / serverTickRate;
        }
    }

    [Header("Simulation")]
    public float maxSimTime = 0.5f;

    [Tooltip("Should be higher than or equal to maxSimTime")]
    public float maxTickHistoryTime = 1.0f;

    [Tooltip("Debug replay")]
    public bool replayMode = false;
    public bool freezeReplay = false;

    public int replayStart;
    public int replayEnd;

    private struct TickState
    {
        /// <summary>
        /// Actual tick
        /// </summary>
        public MsgServerTick tick;

        /// <summary>
        /// Snapshot of the game BEFORE execution of this tick
        /// </summary>
        public Stream snapshot;
    }

    /// <summary>
    /// Server ticks that have either a) been received in the last maxTickHistoryTime or b) not been processed yet (these can be older if that happens)
    /// </summary>
    private List<TickState> serverTickHistory = new List<TickState>();

    /// <summary>
    /// Client ticks received but not yet processed
    /// </summary>
    private List<MsgClientTick>[] pendingClientTicks = new List<MsgClientTick>[maxPlayers];

    private NetworkingManager net;

    public string netStat
    {
        get; private set;
    }

    private void Awake()
    {
        for (int i = 0; i < pendingClientTicks.Length; i++)
            pendingClientTicks[i] = new List<MsgClientTick>();
    }

    private bool InitNet()
    {
        if (net)
            return true;

        net = NetworkingManager.Singleton;

        if (net == null)
        {
            Debug.LogWarning("No network manager found");
            return false;
        }

        // Register network callbacks
        net.OnClientConnectedCallback += OnClientConnected;
        net.OnClientDisconnectCallback += OnClientDisconnected;

        // Register message handlers
        CustomMessagingManager.RegisterNamedMessageHandler("servertick", OnReceivedServerTick);
        CustomMessagingManager.RegisterNamedMessageHandler("serverintro", OnReceivedServerIntro);
        CustomMessagingManager.RegisterNamedMessageHandler("clienttick", OnReceivedClientTick);
        return true;
    }

    /// <summary>
    /// Runs a networking tick
    /// </summary>
    public void Tick()
    {
        if (net == null)
        {
            if (NetworkingManager.Singleton != null)
                net = NetworkingManager.Singleton;
            else
                return;
        }

        if (replayMode && serverTickHistory.Count > 0)
        {
            int startTick = Mathf.Clamp(replayStart, 0, serverTickHistory.Count - 1);
            int endTick = Mathf.Clamp(replayEnd, 0, startTick);

            serverTickHistory[startTick].snapshot.Position = 0;
            Frame.local.Deserialize(serverTickHistory[startTick].snapshot);
            FindObjectOfType<GameHUD>().ClearLog();
            Debug.Log($"SIM@{serverTickHistory[startTick].tick.time.ToString("#.00")}->" +
                $"{(serverTickHistory[endTick].tick.time+serverTickHistory[endTick].tick.deltaTime).ToString("#.00")}");

            Frame.local.isResimulation = true;
            for (int i = startTick; i >= endTick; i--)
            {
                Debug.Log($"PlayerPos{players[0].transform.position}");
                Frame.local.Tick(serverTickHistory[i].tick);
            }
            Frame.local.isResimulation = false;

            if (freezeReplay)
                return;
            Debug.Log($"TICK@{Frame.local.time}->{Frame.local.time + Time.deltaTime}");
        }

        // Generate local inputs
        localInputCmds = InputCmds.FromLocalInput(localInputCmds);

        // Tick the game
        if (net.IsServer)
            TickServer();
        else if (net.IsClient)
            TickClient();

        // Do debug stuff
        UpdateNetStat();
    }

    void TickClient()
    {
        bool doClientTick = true;

        // Send local inputs
        if (doClientTick)
            ClientSendTick();

        // Run received ticks
        for (int i = serverTickHistory.Count - 1; i >= 0; i--)
        {
            TickState tickState = serverTickHistory[i];

            if (tickState.tick.time < Frame.local.time)
                continue;

            // Spawn players who aren't in the game (kinda hacky and temporary-y)
            for (int p = 0; p < players.Length; p++)
            {
                if (players[p] == null && tickState.tick.isPlayerInGame[p])
                    AddPlayer(p);
            }

            // Read syncers
            if (tickState.tick.syncers.Length > 0)
            {
                tickState.tick.syncers.Position = 0;
                while (tickState.tick.syncers.Position < tickState.tick.syncers.Length)
                {
                    int player = tickState.tick.syncers.ReadByte();
                    players[player].movement.ReadSyncer(tickState.tick.syncers);
                }
            }

            // Tick!
            Frame.local.Tick(tickState.tick);
        }

        // Cleanup tick history
        for (int i = 0; i < serverTickHistory.Count; i++)
        {
            if (serverTickHistory[i].tick.time < serverTickHistory[0].tick.time - maxTickHistoryTime)
                serverTickHistory.RemoveRange(i, serverTickHistory.Count - i);
        }
    }

    void TickServer()
    {
        bool doServerTick = true;

        // Run tick locally
        if (doServerTick)
        {
            // Create a new tick
            MsgServerTick tick = MakeServerTick(Time.deltaTime);

            // Receive player inputs into the tick
            for (int i = 0; i < maxPlayers; i++)
            {
                pendingClientTicks[i].Sort((a, b) => (int)(a.deltaTime - b.deltaTime >= 0 ? 1 : -1));

                foreach (MsgClientTick clientTick in pendingClientTicks[i])
                    tick.playerInputs[i] = clientTick.playerInputs;

                pendingClientTicks[i].Clear();
            }

            // Set local inputs in the tick
            if (localPlayerId >= 0)
                tick.playerInputs[localPlayerId] = localInputCmds;

            // Send tick to other players
            ServerSendTick(tick);

            // Take a snapshot of this tick and add to the server tick history
            serverTickHistory.Insert(0, new TickState() { tick = tick, snapshot = Frame.local.Serialize() });

            // Run the tick locally
            Frame.local.Tick(tick);

            // Cleanup tick history
            for (int i = 0; i < serverTickHistory.Count; i++)
            {
                if (serverTickHistory[i].tick.time < serverTickHistory[0].tick.time - maxTickHistoryTime)
                    serverTickHistory.RemoveRange(i, serverTickHistory.Count - i);
            }
        }
    }

    #region Players
    public Player AddPlayer(int id = -1)
    {
        if (id == -1)
        {
            // Find the appropriate ID for this player
            for (id = 0; id < maxPlayers; id++)
            {
                if (players[id] == null)
                    break;
            }
        }

        if (id == maxPlayers)
        {
            Debug.LogWarning("Can't add new player - too many!");
            return null;
        }

        // Spawn the player
        Player player = GameObject.Instantiate(playerPrefab).GetComponent<Player>();

        player.playerId = id;
        players[id] = player;

        player.Rename($"Fred");

        player.Respawn();

        return player;
    }

    public void RemovePlayer(int id)
    {
        if (players[id] != null)
        {
            GameManager.DestroyObject(players[id].gameObject);
            players[id] = null;
        }
    }

    public Player GetPlayerFromClient(ulong clientId)
    {
        return System.Array.Find(players, a => a != null && a.clientId == clientId);
    }
    #endregion

    #region SyncedObjects
    public void RegisterSyncedObject(SyncedObject obj)
    {
        while (obj.syncedId >= syncedObjects.Count)
            syncedObjects.Add(null);

        //Debug.Log($"Registered obj {obj} id {obj.syncedId} tick {Frame.local.time}");
        Debug.Assert(syncedObjects[obj.syncedId] == null);
        syncedObjects[obj.syncedId] = obj;
    }

    public void UnregisterSyncedObject(SyncedObject obj)
    {
        //Debug.Log($"Unregistering obj {obj} id {obj.syncedId} tick {Frame.local.time}");
        Debug.Assert(syncedObjects.Count > obj.syncedId);
        Debug.Assert(syncedObjects[obj.syncedId] == obj);
        syncedObjects[obj.syncedId] = null;
    }
    #endregion

    #region Connection
    public void ConnectToServer(string ipString)
    {
        if (net || InitNet())
        {
            RufflesTransport.RufflesTransport transport = net.NetworkConfig.NetworkTransport as RufflesTransport.RufflesTransport;
            string[] ipPort = ipString.Split(':');

            transport.ConnectAddress = ipPort[0];
            transport.Port = (ushort)(ipPort.Length > 1 ? System.Int32.Parse(ipPort[1]) : 5029);

            connectionStatus = ConnectionStatus.Connecting;

            Debug.Log($"Connecting to {transport.ConnectAddress}:{transport.Port}");
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
            localPlayerId = AddPlayer().playerId;

            net.StartHost();
        }
        else
        {
            Debug.LogWarning("Cannot create server: Net system could not be initialized");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (net.IsServer)
        {
            Debug.Log("A client has connected!");

            // Create their player
            Player player = AddPlayer();

            player.clientId = clientId;

            // Send them the intro packet
            ServerSendIntro(clientId);
        }
        else if (net.IsClient)
        {
            Debug.Log("Connection successful");

            connectionStatus = ConnectionStatus.Ready;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (net.IsServer)
        {
            Debug.Log("A client has disconnected");

            int playerId = GetPlayerFromClient(clientId).playerId;
            RemovePlayer(playerId);
        }
        else if (net.IsClient)
        {
            Debug.Log("Disconnected from server");

            for (int i = 0; i < maxPlayers; i++)
            {
                if (i != localPlayerId && players[i] != null)
                    RemovePlayer(i);
            }

            net.StopClient();
            connectionStatus = ConnectionStatus.Disconnected;
        }
    }
    #endregion

    #region Messaging
    void ServerSendTick(MsgServerTick tick)
    {
        // Make a server tick
        MemoryStream output = new MemoryStream();

        tick.ToStream(output);

        // Send it to all clients
        foreach (var client in net.ConnectedClientsList)
            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, output, "Unreliable");

        numSentTicks++;
    }

    private MsgServerTick MakeServerTick(float deltaTime)
    {
        MsgServerTick tick = new MsgServerTick()
        {
            deltaTime = deltaTime,
            time = Frame.local.time,
            isPlayerInGame = System.Array.ConvertAll<Player, bool>(Netplay.singleton.players, a => a != null)
        };

        // Add syncers
        foreach (Player player in players)
        {
            if (player)
            {
                if ((int)((Frame.local.time - Frame.local.deltaTime) * player.movement.syncsPerSecond) != (int)(Frame.local.time * player.movement.syncsPerSecond))
                {
                    tick.syncers.WriteByte((byte)player.playerId);
                    player.movement.WriteSyncer(tick.syncers);
                }
            }
        }

        return tick;
    }

    private void OnReceivedServerTick(ulong sender, Stream payload)
    {
        if (!net.IsServer)
        {
            // Insert the tick into our history
            MsgServerTick tick = new MsgServerTick(payload);
            int insertIndex = 0;
            for (insertIndex = 0; insertIndex < serverTickHistory.Count - 1; insertIndex++)
            {
                if (serverTickHistory[insertIndex].tick.time < tick.time)
                    break;
            }

            serverTickHistory.Insert(insertIndex, new TickState {
                tick = tick,
                snapshot = null
            });

            // Record STATS!
            numTicksPerFrame[Mathf.Min(netStatFrameNum, numTicksPerFrame.Length - 1)]++;
            numReceivedTicks++;
            numReceivedBytes += (int)payload.Length;
        }
    }

    private void ServerSendIntro(ulong clientId)
    {
        MemoryStream introStream = new MemoryStream(10);

        introStream.WriteByte((byte)GetPlayerFromClient(clientId).playerId);

        CustomMessagingManager.SendNamedMessage("serverintro", clientId, introStream, "Reliable");
    }

    private void OnReceivedServerIntro(ulong sender, Stream payload)
    {
        if (!net.IsServer)
        {
            localPlayerId = payload.ReadByte();

            Debug.Log($"Received server intro, I am player {localPlayerId}");
        }
    }

    void ClientSendTick()
    {
        if (localPlayerId >= 0 && players[localPlayerId])
        {
            Stream inputs = new MemoryStream();
            MsgClientTick clientTick = new MsgClientTick()
            {
                deltaTime = Frame.local.deltaTime,
                playerInputs = localInputCmds,
                serverTime = Frame.local.time
            };

            clientTick.ToStream(inputs);
            CustomMessagingManager.SendNamedMessage("clienttick", net.ServerClientId, inputs, "Unreliable");

            numSentTicks++;
        }
    }

    void OnReceivedClientTick(ulong sender, Stream payload)
    {
        Player player = GetPlayerFromClient(sender);

        // Add this tick to pending list
        if (player != null)
            pendingClientTicks[player.playerId].Add(new MsgClientTick(payload));

        numTicksPerFrame[Mathf.Min(netStatFrameNum, numTicksPerFrame.Length - 1)]++;
        numReceivedBytes += (int)payload.Length;
        numReceivedTicks++;
    }
    #endregion

    #region Debugging
    private float lastClientTickTime = 0;

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
    #endregion
}
