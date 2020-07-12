using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;
using System.IO;

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

    [System.Serializable]
    public struct TickState
    {
        /// <summary>
        /// Actual tick
        /// </summary>
        public MsgTick tick;

        /// <summary>
        /// Snapshot of the game BEFORE execution of this tick
        /// </summary>
        public World state;
    }

    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId = -1;

    /// <summary>
    /// Current local input commands
    /// </summary>
    private InputCmds localInputCmds;

    /// <summary>
    /// Current local player time since...uh...i dunno yet
    /// </summary>
    private float localPlayerTime;

    /// <summary>
    /// Current local player times for other players. Valid on server. For clients only localPlayerId has a valid value
    /// </summary>
    private float[] playerLocalTimes = new float[maxPlayers];

    /// <summary>
    /// Client: The last server tick that was processed
    /// </summary>
    public MsgTick lastProcessedServerTick = null;

    /// <summary>
    /// Server ticks that have either a) been received in the last maxTickHistoryTime or b) not been processed yet (these can be older if that happens)
    /// </summary>
    public List<TickState> serverTickHistory = new List<TickState>();

    /// <summary>
    /// Client ticks received but not yet processed
    /// </summary>
    private List<MsgClientTick>[] pendingClientTicks = new List<MsgClientTick>[maxPlayers];

    /// <summary>
    /// Used by the server. Used to defer new player creation
    /// </summary>
    private ulong[] playerClientIds = new ulong[maxPlayers];

    public int testServerPrediction = 10;

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

        // Generate local inputs
        localInputCmds = InputCmds.FromLocalInput(localInputCmds);

        // Update local time
        localPlayerTime += Time.deltaTime;

        // Send local inputs to the server, if we're a client
        if (net.IsClient && !net.IsServer)
            ClientSendTick();

        // Run some ticks
        int firstServerTick = -1;
        float desiredLocalTickTime = 0;

        // ==== CLIENT TICK ====
        if (!net.IsServer)
        {
            float lastTickTime = lastProcessedServerTick != null ? lastProcessedServerTick.time : 0;

            // Continue to simulation
            if (lastProcessedServerTick != null)
                desiredLocalTickTime = World.server.time + lastProcessedServerTick.deltaTime + localPlayerTime - lastProcessedServerTick.localTime;

            // Find first server tick to start running from
            for (int i = 0; i < serverTickHistory.Count; i++)
            {
                if (serverTickHistory[i].tick.time > lastTickTime)
                    firstServerTick = i;
            }

            // Run any unprocessed server ticks
            if (firstServerTick != -1)
            {
                // Tick!
                for (int i = firstServerTick; i >= 0; i--)
                {
                    World.server.Tick(serverTickHistory[i].tick, false);
                    lastProcessedServerTick = serverTickHistory[i].tick;
                }
            }
        }
        // ==== SERVER ====
        else if (net.IsServer)
        {
            desiredLocalTickTime = World.live.time + Time.deltaTime;

            // Should we make a new tick?
            float closestServerTick = /*(int)(desiredLocalTickTime / serverDeltaTime) * serverDeltaTime*/ serverTickHistory.Count > 0 ? serverTickHistory[0].tick.time : 0;

            if (serverTickHistory.Count == 0 || serverTickHistory[0].tick.time < closestServerTick || true)
            {
                // The next tick will begin at the latest created tick, advanced by serverDeltaTime
                // MakeTick will include the latest controls, etc
                MsgTick nextServerTick = MakeTick(closestServerTick + Time.deltaTime, Time.deltaTime/*serverDeltaTime*/, true, true);

                // Send this tick to other players
                ServerSendTick(nextServerTick);

                //Debug.Log($"Generate {nextServerTick.time}");

                // Insert the new tick
                serverTickHistory.Insert(0, new TickState() { tick = nextServerTick, state = World.live });

                lastProcessedServerTick = serverTickHistory[0].tick;

                // Tick it locally here for now cuz we want the game to up and do something yknow
                int serverTick = Mathf.Min(serverTickHistory.Count - 1, testServerPrediction);

                World.server.Tick(serverTickHistory[serverTick].tick, false);

                World.simulation.CloneFrom(World.server);
                for (int i = serverTick - 1; i >= 0; i--)
                {
                    World.simulation.Tick(serverTickHistory[i].tick, i != 0);
                }
            }
        }

        // Simulate local ticks for real-time response
        /*if (desiredLocalTickTime > World.live.time && serverTickHistory.Count > 0)
        {
            // Make our own fake tick to pass the time
            MsgTick tick = MakeTick(World.live.time, desiredLocalTickTime - World.live.time, false, false);

            //Debug.Log($"Sim {GameState.live.time}->{desiredLocalTickTime}");
            if (net.IsServer && lastProcessedServerTick != null)
            {
                tick.playerInputs = (InputCmds[])lastProcessedServerTick.playerInputs.Clone();
            }
            else if (lastProcessedServerTick != null)
            {
                //tick.playerInputs = (InputCmds[])lastProcessedServerTick.playerInputs.Clone();
            }
            tick.playerInputs[localPlayerId].horizontalAim = localInputCmds.horizontalAim;
            tick.playerInputs[localPlayerId].verticalAim = localInputCmds.verticalAim;

            World.Tick(tick, true, false);
        }*/

        // Cleanup tick history
        CleanupOldServerTicks();

        // Do debug stuff
        UpdateNetStat();
    }

    void CleanupOldServerTicks()
    {
        for (int i = 1; i < serverTickHistory.Count; i++)
        {
            if (serverTickHistory[i].tick.time < serverTickHistory[0].tick.time - maxTickHistoryTime)
                serverTickHistory.RemoveRange(i, serverTickHistory.Count - i);
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

            // Make the first tick
            serverTickHistory.Add(new TickState() { tick = MakeTick(0, 1f / serverTickRate, false, true), state = World.live });
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

        // Send it to all clients
        foreach (var client in net.ConnectedClientsList)
        {
            if (GetPlayerIdFromClient(client.ClientId) == -1)
                continue; // some client IDs are invalid, why? is it myself? then why can I send messages to myself but not receive them?

            output.Position = 0;
            tick.localTime = playerLocalTimes[GetPlayerIdFromClient(client.ClientId)];
            tick.ToStream(output);
            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, output, "Unreliable");
        }

        numSentTicks++;
    }

    private MsgTick MakeTick(float time, float deltaTime, bool includeSyncers, bool isServer)
    {
        MsgTick tick = new MsgTick()
        {
            deltaTime = deltaTime,
            time = time,
            isPlayerInGame = isServer ? System.Array.ConvertAll<ulong, bool>(playerClientIds, a => a != ulong.MaxValue) : System.Array.ConvertAll<Player, bool>(World.server.players, a => a != null)
        };

        if (localPlayerId >= 0 && isServer)
            tick.isPlayerInGame[localPlayerId] = true;

        // Receive player inputs into the tick
        if (isServer)
        {
            for (int p = 0; p < maxPlayers; p++)
            {
                if (pendingClientTicks[p].Count > 0)
                {
                    pendingClientTicks[p].Sort((a, b) => (a.deltaTime - b.deltaTime >= 0 ? 1 : -1));

                    foreach (MsgClientTick clientTick in pendingClientTicks[p])
                        tick.playerInputs[p] = clientTick.playerInputs;

                    pendingClientTicks[p].Clear();
                }
                else if (serverTickHistory.Count > 0)
                {
                    tick.playerInputs[p] = serverTickHistory[0].tick.playerInputs[p];
                }
            }
        }
        tick.playerInputs[localPlayerId] = localInputCmds; // and our own!

        // Add syncers
        if (includeSyncers)
        {
            foreach (Player player in World.server.players)
            {
                if (player)
                {
                    if ((int)((tick.time + deltaTime) * player.movement.syncsPerSecond) != (int)(tick.time * player.movement.syncsPerSecond))
                    {
                        tick.syncers.WriteByte((byte)player.playerId);
                        player.movement.WriteSyncer(tick.syncers);
                    }
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
            MsgTick tick = new MsgTick(payload);
            int insertIndex;
            for (insertIndex = 0; insertIndex < serverTickHistory.Count - 1; insertIndex++)
            {
                if (serverTickHistory[insertIndex].tick.time < tick.time)
                    break;
            }

            serverTickHistory.Insert(insertIndex, new TickState {
                tick = tick
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
                deltaTime = World.live.deltaTime,
                playerInputs = localInputCmds,
                serverTime = World.live.time,
                localTime = localPlayerTime
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
            playerLocalTimes[playerId] = tick.localTime;
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
        if (serverTickHistory.Count > 0 && localPlayerId >= 0)
        {
            return playerLocalTimes[localPlayerId] - serverTickHistory[0].tick.localTime;
        }
        return -1;
    }
    #endregion
}
