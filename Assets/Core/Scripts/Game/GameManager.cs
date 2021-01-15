using Mirror;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public NetGameState defaultNetGameState;

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
            return cachedCamera ?? (cachedCamera = FindObjectOfType<PlayerCamera>());
        }
    }
    private PlayerCamera cachedCamera = null;

    bool isMouseLocked = true;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
    }

    private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
    {
        if (NetworkServer.active)
        {
            NetGameState.SetNetGameState(defaultNetGameState.gameObject);
        }
    }

    void Update()
    {
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