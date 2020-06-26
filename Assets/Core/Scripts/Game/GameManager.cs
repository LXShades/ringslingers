using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using System.IO;
using MLAPI.Messaging;
using System;

public class GameManager : MonoBehaviour
{
    /// <summary>
    /// Holds references to essential objects including the NetworkingManager, etc and advanced game frames.
    /// </summary>
    public static GameManager singleton
    {
        get
        {
            if (_singleton == null)
            {
                _singleton = FindObjectOfType<GameManager>();
            }

            return _singleton;
        }
    }
    private static GameManager _singleton;

    public const int maxPlayers = 16;

    [Header("Frames")]
    /// <summary>
    /// Current frame info. Same as Frame.current
    /// </summary>
    public Frame localFrame = new Frame();

    /// <summary>
    /// Current actual, physical frame info
    /// </summary>
    public Frame serverFrame = new Frame();

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

    [Header("Managers")]
    /// <summary>
    /// Reference to the networking manager
    /// </summary>
    public NetworkingManager net;

    public enum NetConnectStatus
    {
        Ready = 0,
        Connecting = 1,
    };
    public NetConnectStatus connectionStatus;

    [Header("Object lists")]
    /// <summary>
    /// Complete list of syncedObjects in the active scene
    /// </summary>
    public List<SyncedObject> syncedObjects = new List<SyncedObject>();

    [Header("Players")]
    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId = -1;

    /// <summary>
    /// Prefab used to spawn players
    /// </summary>
    public GameObject playerPrefab;

    [Header("Scaling")]
    public float fracunitsPerM = 64;

    /// <summary>
    /// The currently active in-game camera
    /// </summary>
    public new PlayerCamera camera
    {
        get
        {
            return FindObjectOfType<PlayerCamera>(); // prototyping
        }
    }

    public static string[] editorCommandLineArgs
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetString("editorCommandLine", "").Split(' ');
#else
            return new string[0];
#endif
        }
        set
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString("editorCommandLine", string.Join(" ", value));
#endif
        }
    }

    private InputCmds localInputCmds;

    public string netStat
    {
        get; private set;
    }

    /// <summary>
    /// Server ticks that have been received but not processed
    /// </summary>
    private List<ServerTick> pendingServerTicks = new List<ServerTick>();

    bool isMouseLocked = true;

    private void Start()
    {
        ServerTick test = new ServerTick();
        MemoryStream stream = new MemoryStream();
        test.syncers.WriteByte(10);
        test.syncers.WriteByte(2);
        test.ToStream(stream);
        stream.Position = 0;
        test.FromStream(stream);

        // Register network callbacks
        net.OnClientConnectedCallback += OnClientConnected;
        net.OnClientDisconnectCallback += OnClientDisconnected;

        // Register message handlers
        CustomMessagingManager.RegisterNamedMessageHandler("servertick", OnReceivedServerTick);
        CustomMessagingManager.RegisterNamedMessageHandler("serverintro", OnReceivedServerIntro);
        CustomMessagingManager.RegisterNamedMessageHandler("clienttick", OnReceivedClientTick);

        Cursor.lockState = CursorLockMode.Locked;

        // Read command line
        List<string> commandLine = new List<string>(System.Environment.GetCommandLineArgs());

        commandLine.AddRange(editorCommandLineArgs);

        // Connect or host a server
        int connectIndex = commandLine.IndexOf("connect");
        if (connectIndex >= 0 && connectIndex < commandLine.Count - 1)
            ConnectToServer(commandLine[connectIndex + 1]);
        else
            CreateServer();
    }

    void Update()
    {
        bool doServerTick = net.IsServer && Frame.local.time >= Frame.server.time + serverDeltaTime;
        bool doClientTick = !net.IsServer && Frame.local.time >= lastClientTickTime + serverDeltaTime;

        // As the server, we're simulating frames at the max frame rate, but running the actual server at the tick rate
        // Run simulated high-precision frame
        // Send local inputs
        localInputCmds = MakeLocalInputCmds(localInputCmds);
        if (localPlayerId >= 0 && net.IsServer)
            Frame.local.playerInputs[localPlayerId] = localInputCmds;
        
        // Send server tick information before actually running the tick locally
        // This is because the server tick contains the last ticks we received from clients, and may also contain syncs
        // Sending this before the tick means we'll send the sync immediately followed by the tic due to be processed, so that the client can treat it as such
        while (doServerTick)
        {
            Frame.server.time += serverDeltaTime;
            ServerSendTick();
            doServerTick = Frame.local.time + Time.deltaTime >= Frame.server.time + serverDeltaTime;

            doServerTick = false; // tempTEMPOROONI
        }

        if (doClientTick)
        {
            ClientSendTick();
            lastClientTickTime = Mathf.Max(lastClientTickTime + serverDeltaTime, Frame.local.time - serverDeltaTime * 3);
        }

        // Tick the game
        if (net.IsServer)
        {
            Frame.local.Tick(Time.deltaTime);
        }
        else
        {
            pendingServerTicks.Sort((a, b) => (int)(a.time - b.time >= 0 ? 1 : -1));

            foreach (ServerTick tick in pendingServerTicks)
            {
                tick.playerInputs.CopyTo(Frame.local.playerInputs, 0);

                // Spawn players who aren't in the game (kinda hacky and temporary-y)
                for (int i = 0; i < Frame.local.players.Length; i++)
                {
                    if (Frame.local.players[i] == null && tick.isPlayerInGame[i])
                    {
                        Frame.local.players[i] = Instantiate(playerPrefab).GetComponent<Player>();
                        Frame.local.players[i].playerId = i;
                    }
                }

                // Read syncers
                if (tick.syncers.Length > 0)
                {
                    tick.syncers.Seek(0, SeekOrigin.Begin);
                    while (tick.syncers.Position < tick.syncers.Length)
                    {
                        int player = tick.syncers.ReadByte();
                        Frame.local.players[player].movement.ReadSyncer(tick.syncers);
                    }
                }

                // Tick!
                Frame.local.Tick(tick.deltaTime);
            }

            pendingServerTicks.Clear();
        }

        // Do debug stuff
        UpdateNetStat();
        RunDebugCommands();
    }

    #region ObjectReferencing
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

    public Player GetPlayerFromClient(ulong clientId)
    {
        return System.Array.Find(Frame.local.players, a => a != null && a.clientId == clientId);
    }
    #endregion

    #region Networking
    private void ConnectToServer(string ipString)
    {
        RufflesTransport.RufflesTransport transport = net.NetworkConfig.NetworkTransport as RufflesTransport.RufflesTransport;
        string[] ipPort = ipString.Split(':');

        transport.ConnectAddress = ipPort[0];
        transport.Port = (ushort)(ipPort.Length > 1 ? Int32.Parse(ipPort[1]) : 5029);

        connectionStatus = NetConnectStatus.Connecting;

        Debug.Log($"Connecting to {transport.ConnectAddress}:{transport.Port}");
        net.StartClient();
    }

    private void CreateServer()
    {
        // should be Frame.server, serialization/deserialization is still todo
        localPlayerId = Frame.local.CmdAddPlayer().playerId;

        net.StartHost();
    }

    void OnClientConnected(ulong clientId)
    {
        if (net.IsServer)
        {
            Debug.Log("A client has connected!");

            // Create their player
            Player player = Frame.local.CmdAddPlayer();

            player.clientId = clientId;

            // Send them the intro packet
            ServerSendIntro(clientId);
        }
        else if (net.IsClient)
        {
            Debug.Log("Connection successful");

            connectionStatus = NetConnectStatus.Ready;
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (net.IsServer)
            Debug.Log("A client has disconnected");
        else if (net.IsClient)
            Debug.Log("Disconnected from server");
    }

    void ServerSendTick()
    {
        // Make a server tick
        MemoryStream output = new MemoryStream();
        ServerTick tick = new ServerTick()
        {
            deltaTime = Frame.local.deltaTime,
            time = Frame.local.time,
            playerInputs = Frame.local.playerInputs
        };

        // Add syncers
        foreach (Player player in Frame.local.players)
        {
            if (player)
            {
                if ((int)((Frame.server.time - serverDeltaTime) * player.movement.syncsPerSecond) != (int)(Frame.server.time * player.movement.syncsPerSecond))
                {
                    tick.syncers.WriteByte((byte)player.playerId);
                    player.movement.WriteSyncer(tick.syncers);
                }
            }
        }

        tick.ToStream(output);

        // Send it to all clients
        foreach (var client in net.ConnectedClientsList)
        {
            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, output, "Unreliable");
        }

        numSentTicks++;
    }

    private void OnReceivedServerTick(ulong sender, Stream payload)
    {
        if (!net.IsServer)
        {
            pendingServerTicks.Add(new ServerTick(payload));

            numTicksPerFrame[Mathf.Min(netStatFrameNum, numTicksPerFrame.Length - 1)]++;
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
        if (localPlayerId >= 0 && Frame.local.players[localPlayerId])
        {
            Stream inputs = new MemoryStream();

            localInputCmds.ToStream(inputs);
            CustomMessagingManager.SendNamedMessage("clienttick", net.ServerClientId, inputs, "Unreliable");

            numSentTicks++;
        }
    }

    void OnReceivedClientTick(ulong sender, Stream payload)
    {
        Player player = GetPlayerFromClient(sender);

        Debug.Assert(player);

        Frame.local.playerInputs[player.playerId].FromStream(payload);

        numTicksPerFrame[Mathf.Min(netStatFrameNum, numTicksPerFrame.Length - 1)]++;
    }

#endregion

    #region Input
    public InputCmds MakeLocalInputCmds(InputCmds lastInput)
    {
        InputCmds localInput;

        localInput.moveHorizontalAxis = Input.GetAxisRaw("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxisRaw("Vertical");

        localInput.horizontalAim = (lastInput.horizontalAim + Input.GetAxis("Mouse X") % 360 + 360) % 360;
        localInput.verticalAim = Mathf.Clamp(lastInput.verticalAim - Input.GetAxis("Mouse Y"), -89.99f, 89.99f);

        localInput.btnFire = Input.GetButton("Fire");
        localInput.btnJump = Input.GetButton("Jump");

        return localInput;
    }
    #endregion

    #region Debug
    Stream tempSave;

    private float lastClientTickTime = 0;

    private int netStatFrameNum = 0;
    private int[] numTicksPerFrame = new int[500];
    private int numReceivedTicks = 0;
    private int numSentTicks = 0;
    private int numReceivedBytes = 0;

    void RunDebugCommands()
    {
        // Debug controls
        QualitySettings.vSyncCount = 0;
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Application.targetFrameRate = 35;

        if (Input.GetKeyDown(KeyCode.Alpha2))
            Application.targetFrameRate = 60;

        if (Input.GetKeyDown(KeyCode.Alpha3))
            Application.targetFrameRate = 144;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMouseLocked = !isMouseLocked;

            if (isMouseLocked)
                Cursor.lockState = CursorLockMode.Locked;
            else
                Cursor.lockState = CursorLockMode.None;
        }

        // Press F1 to save a state
        if (Input.GetKeyDown(KeyCode.F1))
            tempSave = Frame.local.Serialize();

        if (Input.GetKeyDown(KeyCode.F2) && tempSave != null)
            Frame.local.Deserialize(tempSave);
    }

    void UpdateNetStat()
    {
        netStatFrameNum++;

        // Update netstat
        if ((int)Frame.local.time != (int)(Frame.local.time - Frame.local.deltaTime))
        {
            float averageTicksPerFrame = 0;
            int maxTicksPerFrame = Int32.MinValue, minTicksPerFrame = Int32.MaxValue;
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

            Array.Clear(numTicksPerFrame, 0, numTicksPerFrame.Length);
        }
    }
    #endregion
}

public class ServerTick
{
    public float time;
    public float deltaTime;

    public InputCmds[] playerInputs = new InputCmds[GameManager.maxPlayers];
    public bool[] isPlayerInGame = new bool[GameManager.maxPlayers];

    public MemoryStream syncers = new MemoryStream();

    public ServerTick() { }

    public ServerTick(Stream source)
    {
        FromStream(source);
    }

    public void FromStream(Stream stream)
    {
        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true))
        {
            time = reader.ReadSingle();
            deltaTime = reader.ReadSingle();
        }

        while (stream.Position < stream.Length)
        {
            int player = stream.ReadByte();

            if (player == 255)
                break;

            isPlayerInGame[player] = true;
            playerInputs[player].FromStream(stream);
        }

        syncers = new MemoryStream();

        if (stream.Position < stream.Length)
        {
            stream.CopyTo(syncers, Mathf.Min((int)(stream.Length - stream.Position), 10000));
            syncers.Position = 0;
        }
    }

    public void ToStream(Stream stream)
    {
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(time);
            writer.Write(deltaTime);
        }

        Frame.local.ReadInputs(stream);

        // Add syncers to the message
        stream.WriteByte(255);

        int strlen = (int)stream.Length;

        syncers.WriteTo(stream);
    }
}