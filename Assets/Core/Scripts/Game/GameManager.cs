using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;

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
    public Frame currentFrame = new Frame();

    /// <summary>
    /// Reference to the networking manager
    /// </summary>
    public NetworkingManager net;

    private void Awake()
    {
        // Register network callbacks
        net.OnClientConnectedCallback += OnClientConnected;
        net.OnClientDisconnectCallback += OnClientDisconnected;
    }

    // Update is called once per frame
    void Update()
    {
        Frame.current.Advance(Time.deltaTime);
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
