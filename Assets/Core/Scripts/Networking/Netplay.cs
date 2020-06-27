using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using System.IO;
using Ruffles.Connections;

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

    /// <summary>
    /// Server ticks that have been received but not processed
    /// </summary>
    private List<MsgServerTick> pendingServerTicks = new List<MsgServerTick>();

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

        // There's a lot todo here, don't worry if it doesn't make much sense
        bool doServerTick = net.IsServer;
        bool doClientTick = !net.IsServer && net.IsClient;

        // Generate local inputs
        localInputCmds = InputCmds.FromLocalInput(localInputCmds);

        // Tick the game
        if (net.IsServer)
        {
            // Receive player inputs
            for (int i = 0; i < maxPlayers; i++)
            {
                pendingClientTicks[i].Sort((a, b) => (int)(a.deltaTime - b.deltaTime >= 0 ? 1 : -1));

                foreach (MsgClientTick tick in pendingClientTicks[i])
                    Frame.local.playerInputs[i] = tick.playerInputs;

                pendingClientTicks[i].Clear();
            }

            // Set local inputs
            if (localPlayerId >= 0)
                Frame.local.playerInputs[localPlayerId] = localInputCmds;

            // Run tick locally
            if (doServerTick)
            {
                // Send server inputs
                ServerSendTick(Time.deltaTime);

                Frame.local.Tick(Time.deltaTime);
            }
        }
        else
        {
            // Send local inputs
            if (doClientTick)
                ClientSendTick();

            // Run received ticks
            pendingServerTicks.Sort((a, b) => (int)(a.time - b.time >= 0 ? 1 : -1));

            foreach (MsgServerTick tick in pendingServerTicks)
            {
                // Copy tick to local frame
                tick.playerInputs.CopyTo(Frame.local.playerInputs, 0);
                Frame.local.time = tick.time;

                // Spawn players who aren't in the game (kinda hacky and temporary-y)
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] == null && tick.isPlayerInGame[i])
                        AddPlayer(i);
                }

                // Read syncers
                if (tick.syncers.Length > 0)
                {
                    tick.syncers.Position = 0;
                    while (tick.syncers.Position < tick.syncers.Length)
                    {
                        int player = tick.syncers.ReadByte();
                        players[player].movement.ReadSyncer(tick.syncers);
                    }
                }

                // Tick!
                Frame.local.Tick(tick.deltaTime);
            }

            pendingServerTicks.Clear();
        }

        // Do debug stuff
        UpdateNetStat();
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
                {
                    break;
                }
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

        player.Respawn();

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

    public Player GetPlayerFromClient(ulong clientId)
    {
        return System.Array.Find(players, a => a != null && a.clientId == clientId);
    }

    public void SerializePlayerInputs(Stream output)
    {
        for (int i = 0; i < maxPlayers; i++)
        {
            if (players[i])
            {
                output.WriteByte((byte)i);
                Frame.local.playerInputs[i].ToStream(output);
            }
        }
    }

    public InputCmds[] DeserializePlayerInputs(Stream input, bool[] isPlayerInGame = null)
    {
        InputCmds[] playerInputs = new InputCmds[maxPlayers];

        Debug.Assert(isPlayerInGame == null || isPlayerInGame.Length == maxPlayers);

        if (isPlayerInGame != null)
            System.Array.Clear(isPlayerInGame, 0, isPlayerInGame.Length);

        while (input.Position < input.Length)
        {
            int player = input.ReadByte();

            if (player == 255)
                break;

            if (isPlayerInGame != null)
                isPlayerInGame[player] = true;

            playerInputs[player].FromStream(input);
        }

        return playerInputs;
    }
    #endregion

    #region SyncedObjects
    public void RegisterSyncedObject(SyncedObject obj)
    {
        syncedObjects.Add(obj);
    }

    public void UnregisterSyncedObject(SyncedObject obj)
    {
        int index = syncedObjects.IndexOf(obj);

        if (index >= 0)
        {
            syncedObjects[index] = null;
        }
        else
        {
            Debug.LogWarning("Couldn't unregister synced object: obj not found");
        }
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
    void ServerSendTick(float deltaTime)
    {
        // Make a server tick
        MemoryStream output = new MemoryStream();
        MsgServerTick tick = new MsgServerTick()
        {
            deltaTime = deltaTime,
            time = Frame.local.time,
            playerInputs = Frame.local.playerInputs
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

        tick.ToStream(output);

        // Send it to all clients
        foreach (var client in net.ConnectedClientsList)
            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, output, "Unreliable");

        numSentTicks++;
    }

    private void OnReceivedServerTick(ulong sender, Stream payload)
    {
        if (!net.IsServer)
        {
            pendingServerTicks.Add(new MsgServerTick(payload));

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
