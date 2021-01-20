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
            return cachedCamera != null ? cachedCamera : (cachedCamera = FindObjectOfType<PlayerCamera>());
        }
    }

    public PlayerControls input;

    public bool isPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;

        }
    }
    private bool _isPaused = false;

    private PlayerCamera cachedCamera = null;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        input = new PlayerControls();
        input.Enable();

        GamePreferences.Load(input.asset.FindActionMap("Gameplay").actions.ToArray());
    }

    private void Start()
    {
    }

    void Update()
    {
        Cursor.lockState = (isPaused || SceneManager.GetActiveScene().buildIndex == 1) ? CursorLockMode.None : CursorLockMode.Locked;

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
    }
    #endregion
}