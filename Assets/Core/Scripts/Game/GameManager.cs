using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
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

    [Header("Managers")]
    /// <summary>
    /// Reference to the networking manager
    /// </summary>
    public NetworkingManager net;

    [Header("Game Settings")]
    [Tooltip("How long until items respawn, in seconds")]
    public float itemRespawnTime = 20;

    [Header("Physics")]
    public float gravity = 0.2734375f;

    public float fracunitsPerM = 64;

    /// <summary>
    /// The currently active in-game camera
    /// </summary>
    public new PlayerCamera camera
    {
        get
        {
            return System.Array.Find(FindObjectsOfType<PlayerCamera>(), a => a.worldObject.world == World.server); // prototyping
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

        // Setup the main world with the scene objects
        World.server.CaptureSceneObjects();

        World.simulation.CloneFrom(World.server);
    }

    void Update()
    {
        // Update network
        Netplay.singleton.Tick();

        // Do debug stuff
        RunDebugCommands();
    }

    #region ObjectManagement
    public static GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (World.live != null)
        {
            return World.live.SpawnObject(prefab, position, rotation);
        }
        else
        {
            return Instantiate(prefab, position, rotation);
        }
    }

    public static GameObject SpawnObject(GameObject prefab)
    {
        if (World.live != null)
        {
            return World.live.SpawnObject(prefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            return Instantiate(prefab, Vector3.zero, Quaternion.identity);
        }
    }

    public static void DestroyObject(GameObject obj)
    {
        if (World.live != null)
        {
            World.live.DestroyObject(obj);
        }
        else
        {
            Destroy(obj); // todo
        }
    }
    #endregion

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

        // Press F1 to save a state
        if (Input.GetKeyDown(KeyCode.F1))
        {
            tempSave = World.server.Serialize();
            Debug.Log($"Serialized {tempSave.Length} bytes!");
        }

        if (Input.GetKeyDown(KeyCode.F2) && tempSave != null)
        {
            tempSave.Position = 0;
            World.server.Deserialize(tempSave);
        }
    }
    #endregion
}