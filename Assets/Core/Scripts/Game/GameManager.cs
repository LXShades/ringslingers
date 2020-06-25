using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using System.IO;
using MLAPI.Messaging;
using System;
using RufflesTransport;

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

    bool isMouseLocked = true;

    private void Start()
    {
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

    private float lastClientTickTime = 0;

    private int numReceivedTicks = 0;
    private int numSentTicks = 0;
    private int numReceivedBytes = 0;

    void Update()
    {
        bool doServerTick = net.IsServer && Frame.local.time >= Frame.server.time + serverDeltaTime;
        bool doClientTick = !net.IsServer && Frame.local.time >= lastClientTickTime + serverDeltaTime;

        // As the server, we're simulating frames at the max frame rate, but running the actual server at the tick rate
        // Run simulated high-precision frame
        // Send local inputs
        localInputCmds = MakeLocalInputCmds(localInputCmds);
        if (localPlayerId >= 0 && net.IsServer)
        {
            Frame.local.playerInputs[localPlayerId] = localInputCmds;
        }

        Frame.local.Tick(Time.deltaTime);

        while (doServerTick)
        {
            Frame.server.time += serverDeltaTime;
            ServerSendTick();
            doServerTick = Frame.local.time + Time.deltaTime >= Frame.server.time + serverDeltaTime;
        }

        if (doClientTick)
        {
            ClientSendTick();
            lastClientTickTime = Mathf.Max(lastClientTickTime + serverDeltaTime, Frame.local.time - serverDeltaTime * 3);
        }

        if ((int)Frame.local.time != (int)(Frame.local.time - Frame.local.deltaTime))
        {
            netStat = $"Bytes recv: {numReceivedBytes}\nTicks recv: {numReceivedTicks}\nTicks sent: {numSentTicks}";
            numSentTicks = 0;
            numReceivedTicks = 0;
            numReceivedBytes = 0;
        }
        /*{
            // Run server frame
            float oldLocalTime = Frame.local.time;

            // Rewind to the last server frame

            // Advance the server frame
            while (Frame.local.time + Time.deltaTime >= Frame.server.time + Frame.tickDeltaTime)
            {
                Frame.server.playerInputs[localPlayerId] = MakeLocalInputCmds(Frame.local.playerInputs[localPlayerId]);
                Frame.server.Tick(Frame.tickDeltaTime);
            }

            // We're now at the server frame's time
            Frame.local.time = Frame.server.time;

            // Advance the remaining time
            Frame.local.Tick(oldLocalTime - Frame.local.time + Time.deltaTime);
        }*/

        RunDebugCommands();
    }

    public Player GetPlayerFromClient(ulong clientId)
    {
        return System.Array.Find(Frame.local.players, a => a != null && a.clientId == clientId);
    }

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

    private int numTicksSent = 0;

    void ServerSendTick()
    {
        Stream stream = Frame.local.ReadInputs();

        // Add syncers to the message
        stream.WriteByte(255);

        long sz = stream.Length;
        foreach (Player player in Frame.local.players)
        {
            if (player)
            {
                if ((int)((Frame.server.time - serverDeltaTime) * player.movement.syncsPerSecond) != (int)(Frame.server.time * player.movement.syncsPerSecond))
                {
                    stream.WriteByte((byte)player.playerId);
                    player.movement.WriteSyncer(stream);
                }
            }
        }

        if (stream.Length != sz)
        {
            Debug.Log($"Wrote syncers");
        }

        // Send it to all clients
        foreach (var client in net.ConnectedClientsList)
        {
            CustomMessagingManager.SendNamedMessage("servertick", client.ClientId, stream, "Unreliable");
        }
    }

    private void OnReceivedServerTick(ulong sender, Stream payload)
    {
        if (!net.IsServer)
        {
            while (payload.Position < payload.Length)
            {
                int player = payload.ReadByte();

                if (player == 255)
                    break;

                Frame.local.playerInputs[player].FromStream(payload);

                // If player isn't in the game, spawn them or some shiznit
                if (Frame.local.players[player] == null)
                {
                    Frame.local.players[player] = Instantiate(playerPrefab).GetComponent<Player>();
                    Frame.local.players[player].playerId = player;
                }
            }

            if (payload.Position < payload.Length)
            {
                Debug.Log($"Received syncs at {Frame.local.time}");
            }

            while (payload.Position < payload.Length)
            {
                int player = payload.ReadByte();
                Frame.local.players[player].movement.ReadSyncer(payload);
            }

            numReceivedBytes += (int)payload.Length;
            numReceivedTicks++;
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

    void RunDebugCommands()
    {
        // Debug controls
        if (Input.GetKeyDown(KeyCode.E))
            Application.targetFrameRate = 35;

        if (Input.GetKeyDown(KeyCode.Q))
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
    #endregion
}
