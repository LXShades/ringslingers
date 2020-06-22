using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using System.IO;

public class GameManager : NetworkedBehaviour
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

    /// <summary>
    /// Current frame info. Same as Frame.current
    /// </summary>
    public Frame localFrame = new Frame();

    /// <summary>
    /// Current actual, physical frame info
    /// </summary>
    public Frame serverFrame = new Frame();

    /// <summary>
    /// Reference to the networking manager
    /// </summary>
    public NetworkingManager net;

    /// <summary>
    /// Complete list of syncedObjects in the active scene
    /// </summary>
    public List<SyncedObject> syncedObjects = new List<SyncedObject>();

    private void Awake()
    {
        // Register network callbacks
        net.OnClientConnectedCallback += OnClientConnected;
        net.OnClientDisconnectCallback += OnClientDisconnected;
    }

    GameState tempSave;

    // Update is called once per frame
    void Update()
    {
        // As the server, we're simulating frames at the max frame rate, but running the actual server at the tick rate
        if (Frame.local.time + Time.deltaTime < Frame.server.time + Frame.serverDeltaTime)
        {
            Frame.local.Advance(Time.deltaTime);
        }
        else
        {
            float oldLocalTime = Frame.local.time;

            // Rewind to the server frame

            // Advance the server frame
            while (Frame.local.time + Time.deltaTime >= Frame.server.time + Frame.serverDeltaTime)
            {
                Frame.server.Advance(Frame.serverDeltaTime);
            }

            // We're now at the server frame's time
            Frame.local.time = Frame.server.time;

            // Advance the remaining time
            Frame.local.Advance(oldLocalTime - Frame.local.time + Time.deltaTime);
        }

        // Press F1 to save a state
        if (Input.GetKeyDown(KeyCode.F1))
        {
            tempSave = new GameState();
            
            tempSave.Serialize();
        }

        if (Input.GetKeyDown(KeyCode.F2) && tempSave != null)
        {
            tempSave.Deserialize();
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            // ...
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            // ...
        }
    }
}
