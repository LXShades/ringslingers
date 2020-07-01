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

    public struct TickState
    {
        /// <summary>
        /// Actual tick
        /// </summary>
        public MsgTick tick;

        /// <summary>
        /// Snapshot of the game BEFORE execution of this tick
        /// </summary>
        public Stream snapshot;
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
    /// Current player times. Valid on server. For clients only localPlayerId has a valid value
    /// </summary>
    private float[] localPlayerTimes = new float[maxPlayers];

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
        {
            return;
        }

        // Generate local inputs
        localInputCmds = InputCmds.FromLocalInput(localInputCmds);

        // Update local time
        if (localPlayerId >= 0)
            localPlayerTimes[localPlayerId] += Time.deltaTime;

        // Tick the game
        if (net.IsServer)
            TickServer();
        else if (net.IsClient)
            TickClient();

        // Cleanup tick history
        // server should always store the last tick hence i=1
        for (int i = 1; i < serverTickHistory.Count; i++)
        {
            if (serverTickHistory[i].tick.time < serverTickHistory[0].tick.time - maxTickHistoryTime)
                serverTickHistory.RemoveRange(i, serverTickHistory.Count - i);
        }

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
        if (serverTickHistory.Count > 0)
        {
            for (int i = serverTickHistory.Count - 1; i >= 0; i--)
            {
                TickState tickState = serverTickHistory[i];

                if (tickState.tick.time < Frame.local.time)
                    continue;

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
        }
    }

    private InputCmds[] nextServerTickInputs;

    void TickServer()
    {
        float timeUntilNextServerTick = 0;
        float desiredTime = Frame.local.time + Time.deltaTime;

        if (serverTickHistory.Count > 0)
            timeUntilNextServerTick = (1f/serverTickRate) - (desiredTime - (serverTickHistory[0].tick.time + serverTickHistory[0].tick.deltaTime));

        // Make server tick(s) if possible
        for (int iterations = 0; iterations < 3 && timeUntilNextServerTick <= 0; iterations++ )
        {
            // Rewind to the last server tick if it exists
            if (serverTickHistory.Count > 0)
            {
                serverTickHistory[0].snapshot.Position = 0;
                Frame.local.Deserialize(serverTickHistory[0].snapshot);
            }

            // Make the next server tick with the fixed delta time
            MsgTick serverTick = MakeTick(Frame.local.time, 1f / serverTickRate, true);

            // Give it our commands (psst, player commands as well?)
            if (nextServerTickInputs != null)
                serverTick.playerInputs = nextServerTickInputs;

            // Send this exact tick to other players
            ServerSendTick(serverTick);

            // Tick it locally
            Frame.local.Tick(serverTick);

            // Take a snapshot of this tick and add to the server tick history
            serverTickHistory.Insert(0, new TickState() { tick = serverTick, snapshot = Frame.local.Serialize() });

            timeUntilNextServerTick += 1f / serverTickRate;

            // Record inputs to occur during this next tick
            nextServerTickInputs = (InputCmds[])serverTick.playerInputs.Clone();

            if (localPlayerId >= 0)
                nextServerTickInputs[localPlayerId] = localInputCmds;

            // Receive player inputs into the tick
            for (int i = 0; i < maxPlayers; i++)
            {
                pendingClientTicks[i].Sort((a, b) => (a.deltaTime - b.deltaTime >= 0 ? 1 : -1));

                foreach (MsgClientTick clientTick in pendingClientTicks[i])
                    nextServerTickInputs[i] = clientTick.playerInputs;

                pendingClientTicks[i].Clear();
            }
        }

        // Now run a local tick for smoothy smoothy movey
        Debug.Assert(desiredTime - Frame.local.time >= 0 && desiredTime - Frame.local.time < 1);
        MsgTick tick = MakeTick(Frame.local.time, desiredTime - Frame.local.time, false);

        // For now use the inputs at the server tickrate
        // in the future, I'd prefer to have an instant response
        tick.playerInputs = nextServerTickInputs; // NVM lol

        // Run the tick locally
        Frame.local.isResimulation = true;
        Frame.local.Tick(tick);
        Frame.local.isResimulation = false;
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
        int index = System.Array.IndexOf(playerClientIds, clientId);
        return index >= 0 ? players[index] : null;
    }

    public int GetPlayerIdFromClient(ulong clientId)
    {
        int index = System.Array.IndexOf(playerClientIds, clientId);
        return index;
    }
    #endregion

    #region SyncedObjects
    public void RegisterSyncedObject(SyncedObject obj)
    {
        while (obj.syncedId >= syncedObjects.Count)
            syncedObjects.Add(null);

        //Debug.Log($"Registered obj {obj} id {obj.syncedId} tick {Frame.local.time}");
        //Debug.Assert(syncedObjects[obj.syncedId] == null);
        syncedObjects[obj.syncedId] = obj;
    }

    public void UnregisterSyncedObject(SyncedObject obj)
    {
        //Debug.Log($"Unregistering obj {obj} id {obj.syncedId} tick {Frame.local.time}");
        //Debug.Assert(syncedObjects.Count > obj.syncedId);
        //Debug.Assert(syncedObjects[obj.syncedId] == obj);
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
            tick.localTime = localPlayerTimes[GetPlayerIdFromClient(client.ClientId)];
            tick.ToStream(output);
            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, output, "Unreliable");
        }

        numSentTicks++;
    }

    private MsgTick MakeTick(float time, float deltaTime, bool includeSyncers)
    {
        MsgTick tick = new MsgTick()
        {
            deltaTime = deltaTime,
            time = time,
            isPlayerInGame = System.Array.ConvertAll<ulong, bool>(playerClientIds, a => a != ulong.MaxValue)
        };

        if (localPlayerId >= 0)
            tick.isPlayerInGame[localPlayerId] = true;

        // Add syncers
        if (includeSyncers)
        {
            foreach (Player player in players)
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
        Debug.Log("I received it");
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
        if (localPlayerId >= 0 && players[localPlayerId])
        {
            Stream inputs = new MemoryStream();
            MsgClientTick clientTick = new MsgClientTick()
            {
                deltaTime = Frame.local.deltaTime,
                playerInputs = localInputCmds,
                serverTime = Frame.local.time,
                localTime = localPlayerTimes[localPlayerId]
            };

            clientTick.ToStream(inputs);
            CustomMessagingManager.SendNamedMessage("clienttick", net.ServerClientId, inputs, "Unreliable");

            numSentTicks++;
        }
    }

    void OnReceivedClientTick(ulong sender, Stream payload)
    {
        Player player = GetPlayerFromClient(sender);

        if (player != null)
        {
            // Add this tick to pending list
            MsgClientTick tick = new MsgClientTick(payload);
            pendingClientTicks[player.playerId].Add(tick);
            localPlayerTimes[player.playerId] = tick.localTime;
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

        serverTickHistory[startTick].snapshot.Position = 0;
        Frame.local.Deserialize(serverTickHistory[startTick].snapshot);

        //            Debug.Log($"SIM@{serverTickHistory[startTick].tick.time.ToString("#.00")}->" +
        //                $"{(serverTickHistory[endTick].tick.time+serverTickHistory[endTick].tick.deltaTime).ToString("#.00")}");

        Frame.local.isResimulation = true;
        for (int i = startTick; i >= endTick; i--)
        {
            //                Debug.Log($"PlayerPos{players[0].transform.position}");
            Frame.local.Tick(serverTickHistory[i].tick);
        }
        Frame.local.isResimulation = false;

        return freezeReplay;
        //            Debug.Log($"TICK@{Frame.local.time}->{Frame.local.time + Time.deltaTime}");
    }

    public float GetPing()
    {
        if (serverTickHistory.Count > 0 && localPlayerId >= 0)
        {
            return localPlayerTimes[localPlayerId] - serverTickHistory[0].tick.localTime;
        }
        return -1;
    }
    #endregion
}
