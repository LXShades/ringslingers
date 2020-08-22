using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using System.IO;
using UnityEngine.Audio;

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

    [Header("Players")]
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

    /// <summary>
    /// How regularly clients send ticks
    /// </summary>
    public float clientTickRate = 20;

    /// <summary>
    /// How far the server has simulated ahead
    /// </summary>
    public float serverSimTime = 0.3f;

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

    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId = -1;

    /// <summary>
    /// Current local input commands
    /// </summary>
    private PlayerTick localPlayerTick;

    /// <summary>
    /// Current commands recieved from clients
    /// </summary>
    private PlayerTick[] clientTicks = new PlayerTick[maxPlayers];

    /// <summary>
    /// Client: The last server tick that was processed
    /// </summary>
    public MsgTick lastReceivedServerTick = null;

    /// <summary>
    /// Server ticks that have either a) been received in the last maxTickHistoryTime or b) not been processed yet (these can be older if that happens)
    /// </summary>
    public List<MsgTick> serverTickHistory = new List<MsgTick>();

    /// <summary>
    /// Recorded ticks from a backlog of local simulations
    /// </summary>
    public List<MsgTick> localTickHistory = new List<MsgTick>();

    /// <summary>
    /// Client ticks received but not yet processed
    /// </summary>
    private List<MsgClientTick>[] pendingClientTicks = new List<MsgClientTick>[maxPlayers];

    /// <summary>
    /// Used by the server. Used to defer new player creation
    /// </summary>
    private ulong[] playerClientIds = new ulong[maxPlayers];

    /// <summary>
    /// Whether this is the server player
    /// </summary>
    public bool isServer => net.IsServer;

    /// <summary>
    /// Whether this is not a server or host player
    /// </summary>
    public bool isClient => net.IsClient && !net.IsHost;

    private NetworkingManager net;

    public string netStat
    {
        get; private set;
    }

    private void Awake()
    {
        for (int i = 0; i < pendingClientTicks.Length; i++)
            pendingClientTicks[i] = new List<MsgClientTick>();
        for (int i = 0; i < playerClientIds.Length; i++)
            playerClientIds[i] = ulong.MaxValue;
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

        if (TryReplayMode())
            return;

        if (serverTickHistory.Count > 0 && serverTickHistory[0] != lastReceivedServerTick)
            lastReceivedServerTick = serverTickHistory[0];

        // [SERVER/CLIENT] Receive client ticks and generate local ones (function split?)
        ReceiveClientTicks();

        // [CLIENT] Send client inputs to server
        if (!net.IsServer && (int)(World.live.localTime * clientTickRate) != (int)((World.live.localTime + Time.deltaTime) * clientTickRate))
            ClientSendTick();

        // Client/Server ticking process
        MsgTick serverTickToRun = lastReceivedServerTick;

        if (net.IsServer && ((int)(World.live.localTime * serverTickRate) != (int)((World.live.localTime + Time.deltaTime) * serverTickRate) || lastReceivedServerTick == null))
        {
            // [SERVER] Make a server tick occasionally as the server
            serverTickToRun = MakeTick(World.live.gameTime);
            ServerSendTick(serverTickToRun);
            serverTickHistory.Insert(0, serverTickToRun);
        }

        // [SERVER/CLIENT] Tick the game locally!
        World.live.Tick(MakeTick(World.live.gameTime), serverTickToRun, Time.deltaTime); // run a server tick

        // Cleanup old ticks
        CleanupOutdatedTickHistory();

        // Do debug stuff
        UpdateNetStat();
    }

    void CleanupOutdatedTickHistory()
    {
        for (int i = 1; i < serverTickHistory.Count; i++)
        {
            if (serverTickHistory[i].gameTime < serverTickHistory[0].gameTime - maxTickHistoryTime)
                serverTickHistory.RemoveRange(i, serverTickHistory.Count - i);
        }

        for (int i = 0; i < localTickHistory.Count; i++)
        {
            if (localTickHistory[i].gameTime < World.live.gameTime - maxSimTime)
            {
                localTickHistory.RemoveAt(i--);
            }
        }
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
            localPlayerId = 0;

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

            for (int i = 0; i < maxPlayers; i++)
            {
                if (i != localPlayerId && playerClientIds[i] == ulong.MaxValue)
                {
                    playerClientIds[i] = clientId;
                    ServerSendIntro(clientId);
                    break;
                }
            }
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

            for (int i = 0; i < maxPlayers; i++)
            {
                if (playerClientIds[i] == clientId)
                    playerClientIds[i] = ulong.MaxValue;
            }
        }
        else if (net.IsClient)
        {
            Debug.Log("Disconnected from server");

            net.StopClient();
            connectionStatus = ConnectionStatus.Disconnected;
        }
    }
    #endregion

    #region Messaging
    void ServerSendTick(MsgTick tick)
    {
        // Make a server tick
        MemoryStream output = new MemoryStream();

        tick.ToStream(output);

        // Send it to all clients
        foreach (var client in net.ConnectedClientsList)
        {
            int playerId = GetPlayerIdFromClient(client.ClientId);
            if (playerId == -1 || !World.live.players[playerId])
                continue; // some client IDs are invalid, why? is it myself? then why can I send messages to myself but not receive them?

            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, output, "Unreliable");
        }

        numSentTicks++;
    }

    /// <summary>
    /// Makes a new tick
    /// </summary>
    /// <param name="time"></param>
    /// <param name="isServer">Whether we are the server. If we are the server, information will be lifted from the game world and client ticks.</param>
    /// <returns></returns>
    private MsgTick MakeTick(float gameTime)
    {
        MsgTick tick = new MsgTick();

        tick.gameTime = gameTime;

        // Insert player updates into the tick
        for (int p = 0; p < maxPlayers; p++)
        {
            if (p != localPlayerId)
            {
                if (net.IsServer)
                {
                    // [server] use last received client tick for this player
                    tick.playerTicks[p] = clientTicks[p];

                    // but modify the player's current position etc
                    // this information is slightly out of date though. sigh...
                    if (World.live.players[p])
                    {
                        tick.playerTicks[p].input = World.live.players[p].movement.inputHistory.Count > 0 ? World.live.players[p].movement.inputHistory[0] : new PlayerInput();
                        tick.playerTicks[p].position = World.live.players[p].movement.serverPosition;
                        tick.playerTicks[p].velocity = World.live.players[p].movement.serverVelocity;
                        tick.playerTicks[p].localTime = World.live.players[p].movement.serverLocalTime;
                    }
                }
                else
                {
                    // [client] use last received server tick for this player
                    if (lastReceivedServerTick != null)
                        tick.playerTicks[p] = lastReceivedServerTick.playerTicks[p];
                }
            }
            else if (p == localPlayerId)
            {
                // local player
                tick.playerTicks[p] = localPlayerTick;
            }
        }

        return tick;
    }

    private PlayerTick MakeLocalPlayerTick()
    {
        int playerId = localPlayerId;

        if (playerId >= 0)
        {
            return new PlayerTick()
            {
                input = PlayerInput.FromLocalInput(localPlayerTick.input),
                localTime = World.live.localTime,
                position = World.live.players[playerId] ? World.live.players[playerId].transform.position : Vector3.zero,
                velocity = World.live.players[playerId] ? World.live.players[playerId].movement.velocity : Vector3.zero,
                state = World.live.players[playerId] ? World.live.players[playerId].movement.state : 0,
                isInGame = true
            };
        }
        else
        {
            return new PlayerTick()
            {
                isInGame = false
            };
        }
    }

    private void ReceiveClientTicks()
    {
        for (int p = 0; p < maxPlayers; p++)
        {
            // [SERVER] Receive and apply player inputs into the tick
            if (pendingClientTicks[p].Count > 0)
            {
                float highestTime = float.MinValue;
                MsgClientTick latestClientTick = null;

                foreach (MsgClientTick clientTick in pendingClientTicks[p])
                {
                    if (clientTick.tick.localTime > highestTime)
                    {
                        latestClientTick = clientTick;
                        highestTime = clientTick.tick.localTime;
                    }
                }

                clientTicks[p] = latestClientTick.tick;
                pendingClientTicks[p].Clear();
            }
        }

        // Receive local player tick
        localPlayerTick = MakeLocalPlayerTick();
    }

    private void OnReceivedServerTick(ulong sender, Stream payload)
    {
        if (!net.IsServer)
        {
            // Insert the tick into our history
            MsgTick tick = new MsgTick(payload);
            int insertIndex;
            for (insertIndex = 0; insertIndex < serverTickHistory.Count - 1; insertIndex++)
            {
                if (serverTickHistory[insertIndex].gameTime < tick.gameTime)
                    break;
            }

            serverTickHistory.Insert(insertIndex, tick);

            // Record STATS!
            numTicksPerFrame[Mathf.Min(netStatFrameNum, numTicksPerFrame.Length - 1)]++;
            numReceivedTicks++;
            numReceivedBytes += (int)payload.Length;
        }
    }

    private void ServerSendIntro(ulong clientId)
    {
        MemoryStream introStream = new MemoryStream(10);

        for (int i = 0; i < maxPlayers; i++)
        {
            if (playerClientIds[i] == clientId)
            {
                introStream.WriteByte((byte)i);
                break;
            }
        }

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
        if (localPlayerId >= 0)
        {
            Stream inputs = new MemoryStream();
            MsgClientTick clientTick = new MsgClientTick()
            {
                serverTime = World.live.gameTime,
                tick = localPlayerTick
            };

            clientTick.ToStream(inputs);
            CustomMessagingManager.SendNamedMessage("clienttick", net.ServerClientId, inputs, "Unreliable");

            numSentTicks++;
        }
    }

    void OnReceivedClientTick(ulong sender, Stream payload)
    {
        int playerId = GetPlayerIdFromClient(sender);

        if (playerId != -1)
        {
            // Add this tick to pending list
            MsgClientTick tick = new MsgClientTick(payload);
            pendingClientTicks[playerId].Add(tick);
        }

        // Update stats
        numTicksPerFrame[Mathf.Min(netStatFrameNum, numTicksPerFrame.Length - 1)]++;
        numReceivedBytes += (int)payload.Length;
        numReceivedTicks++;
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

    bool TryReplayMode()
    {
        if (!replayMode || serverTickHistory.Count <= 0)
            return false;

        int startTick = Mathf.Clamp(replayStart, 0, serverTickHistory.Count - 1);
        int endTick = Mathf.Clamp(replayEnd, 0, startTick);

        //World.live.LoadState(serverTickHistory[startTick].state);

        //            Debug.Log($"SIM@{serverTickHistory[startTick].tick.time.ToString("#.00")}->" +
        //                $"{(serverTickHistory[endTick].tick.time+serverTickHistory[endTick].tick.deltaTime).ToString("#.00")}");

        for (int i = startTick; i >= endTick; i--)
        {
            //                Debug.Log($"PlayerPos{players[0].transform.position}");
        //    World.live.Tick(serverTickHistory[i].tick, true, false);
        }

        return freezeReplay;
        //            Debug.Log($"TICK@{Frame.local.time}->{Frame.local.time + Time.deltaTime}");
    }

    public float GetPing()
    {
        if (lastReceivedServerTick != null && localPlayerId > 0f)
        {
            return World.live.localTime - lastReceivedServerTick.playerTicks[localPlayerId].localTime;
        }
        else
        {
            return 0f;
        }
    }
    #endregion
}
