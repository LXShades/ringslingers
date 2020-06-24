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

    [Header("Managers")]
    /// <summary>
    /// Reference to the networking manager
    /// </summary>
    public NetworkingManager net;

    [Header("Object lists")]
    /// <summary>
    /// Complete list of syncedObjects in the active scene
    /// </summary>
    public List<SyncedObject> syncedObjects = new List<SyncedObject>();

    [Header("Players")]
    /// <summary>
    /// Local player ID
    /// </summary>
    public int localPlayerId;

    /// <summary>
    /// Prefab used to spawn players
    /// </summary>
    public GameObject playerPrefab;

    private void Awake()
    {
        // Register network callbacks
        net.OnClientConnectedCallback += OnClientConnected;
        net.OnClientDisconnectCallback += OnClientDisconnected;

        // Register message handlers
        CustomMessagingManager.RegisterNamedMessageHandler("servertick", OnReceivedServerTick);

#if UNITY_EDITOR
        if (UnityEditor.EditorPrefs.GetBool("netCurrentlyEditorTesting") && false)
        {
            Debug.Log("Client detected, attempting to join 127.0.0.1");
            MLAPI.Transports.UNET.UnetTransport transport = net.NetworkConfig.NetworkTransport as MLAPI.Transports.UNET.UnetTransport;

            transport.ConnectAddress = "127.0.0.1";
            net.StartClient();
        }
        else
        {
#endif
            Debug.Log("Starting host");

            // should be Frame.server, serialization/deserialization is still todo
            localPlayerId = Frame.local.CmdAddPlayer().playerId;
#if UNITY_EDITOR
        }
#endif
    }

    Stream tempSave;

    // Update is called once per frame
    void Update()
    {
        // As the server, we're simulating frames at the max frame rate, but running the actual server at the tick rate
        if (Frame.local.time + Time.deltaTime < Frame.server.time + Frame.tickDeltaTime || true) // DISABLED
        {
            // Run simulated high-precision frame
            Frame.local.playerInputs[localPlayerId] = MakeLocalInputCmds();

            Frame.local.Tick(Time.deltaTime);

            // Debug controls
            if (Input.GetKeyDown(KeyCode.E))
            {
                Application.targetFrameRate = 35;
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Application.targetFrameRate = 144;
            }
        }
        else
        {
            // Run server frame
            float oldLocalTime = Frame.local.time;

            // Rewind to the last server frame

            // Advance the server frame
            while (Frame.local.time + Time.deltaTime >= Frame.server.time + Frame.tickDeltaTime)
            {
                Frame.server.playerInputs[localPlayerId] = MakeLocalInputCmds();
                Frame.server.Tick(Frame.tickDeltaTime);

                if (net.IsServer)
                {
                    foreach (var client in net.ConnectedClientsList)
                    {
                        CustomMessagingManager.SendNamedMessage("serverticks", client.ClientId, Frame.server.ReadInputs());
                    }
                }
            }

            // We're now at the server frame's time
            Frame.local.time = Frame.server.time;

            // Advance the remaining time
            Frame.local.Tick(oldLocalTime - Frame.local.time + Time.deltaTime);
        }

        // Press F1 to save a state
        if (Input.GetKeyDown(KeyCode.F1))
        {
            tempSave = Frame.local.Serialize();
        }

        if (Input.GetKeyDown(KeyCode.F2) && tempSave != null)
        {
            Frame.local.Deserialize(tempSave);
        }
    }

    #region Networking
    void OnClientConnected(ulong clientId)
    {
        if (net.IsServer)
        {
            Debug.Log("A client has connected!");
        }
        else if (net.IsClient)
        {
            Debug.Log("Connection successful");
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (net.IsServer)
        {
            Debug.Log("A client has disconnected");
        }
        else if (net.IsClient)
        {
            Debug.Log("Disconnected from server");
        }
    }

    private void OnReceivedServerTick(ulong sender, Stream payload)
    {

    }
    #endregion

    #region Input
    public InputCmds MakeLocalInputCmds()
    {
        InputCmds localInput;

        localInput.moveHorizontalAxis = Input.GetAxisRaw("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxisRaw("Vertical");

        localInput.lookHorizontalAxis = Input.GetAxis("Mouse X");
        localInput.lookVerticalAxis = -Input.GetAxis("Mouse Y");

        localInput.btnFire = Input.GetButton("Fire");
        localInput.btnJump = Input.GetButton("Jump");

        return localInput;
    }
    #endregion
}
