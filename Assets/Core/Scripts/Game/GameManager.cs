using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Game Manager handles references to key local objects and other non-networking stuff
/// </summary>
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
                _singleton = FindObjectOfType<GameManager>();

            return _singleton;
        }
    }
    private static GameManager _singleton;

    [Header("Game Settings")]
    [Tooltip("How long until items respawn, in seconds")]
    public float itemRespawnTime = 20;
    public GameObject playerPrefab;

    [Header("Physics")]
    public float gravity = 0.2734375f;

    public float fracunitsPerM = 64;

    public GameObject localObjects;

    /// <summary>
    /// The currently active in-game camera
    /// </summary>
    public new PlayerCamera camera
    {
        get
        {
            return System.Array.Find(FindObjectsOfType<PlayerCamera>(), a => a.worldObject.world == World.live); // prototyping
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

    bool isMouseLocked = true;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // Read command line
        List<string> commandLine = new List<string>(System.Environment.GetCommandLineArgs());

        commandLine.AddRange(editorCommandLineArgs);

        // Connect or host a server
        int connectIndex = commandLine.IndexOf("connect");
        if (connectIndex >= 0 && connectIndex < commandLine.Count - 1)
            Netplay.singleton.ConnectToServer(commandLine[connectIndex + 1]);
        else
            Netplay.singleton.CreateServer();
    }

    void Update()
    {
        // Update network
        World.live.Tick(Time.deltaTime);

        Netplay.singleton.Tick();

        // Do debug stuff
        RunDebugCommands();
    }

    #region Debug
    MemoryStream tempSave;

    void RunDebugCommands()
    {
        // Debug controls
        QualitySettings.vSyncCount = 0;
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Application.targetFrameRate = 10;
        if (Input.GetKeyDown(KeyCode.Alpha2))
            Application.targetFrameRate = 30;

        if (Input.GetKeyDown(KeyCode.Alpha3))
            Application.targetFrameRate = 60;

        if (Input.GetKeyDown(KeyCode.Alpha4))
            Application.targetFrameRate = 144;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMouseLocked = !isMouseLocked;

            if (isMouseLocked)
                Cursor.lockState = CursorLockMode.Locked;
            else
                Cursor.lockState = CursorLockMode.None;
        }
    }
    #endregion
}