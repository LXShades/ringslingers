using UnityEngine;
using UnityEngine.InputSystem;
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

    [Header("Player settings")]
    public CharacterNamePair[] playerCharacters;

    [Header("Level Settings")]
    public string defaultMenuScene;

    public LevelDatabase levelDatabase;

    [Tooltip("How long until items respawn, in seconds")]
    public float itemRespawnTime = 20;

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

    private bool areInputsEnabled = false;
    private bool doSuppressGameplayInputs = false;

    private string defaultMenuScenePath;

    private PlayerCamera cachedCamera = null;

    private void Awake()
    {
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        input = new PlayerControls();
        EnableInputs();

        GamePreferences.Load(input.asset.FindActionMap("Gameplay").actions.ToArray());

        defaultMenuScenePath = SceneManager.GetSceneByName(defaultMenuScene).path;
    }

    private void Start()
    {
        NetMan.singleton.onClientDisconnect += OnClientDisconnected;
    }

    void Update()
    {
        Cursor.lockState = (isPaused || string.Compare(SceneManager.GetActiveScene().path, defaultMenuScenePath, true) == 0) ? CursorLockMode.None : CursorLockMode.Locked;

        // Do debug stuff
        RunDebugCommands();
    }

    private void LateUpdate()
    {
        if (doSuppressGameplayInputs)
            DisableInputs();
        else
            EnableInputs();
        doSuppressGameplayInputs = false;
    }

    private void EnableInputs()
    {
        if (!areInputsEnabled)
        {
            input.Enable();
            areInputsEnabled = true;
        }
    }

    private void DisableInputs()
    {
        if (areInputsEnabled)
        {
            input.Disable();
            areInputsEnabled = false;
        }
    }

    private void OnClientDisconnected(Mirror.NetworkConnection conn)
    {
        // go back to the main menu
        SceneManager.LoadSceneAsync(defaultMenuScene);
    }

    public void SuppressGameplayInputs()
    {
        doSuppressGameplayInputs = true;
    }

    #region Debug
    void RunDebugCommands()
    {
        // Debug controls
        if (Keyboard.current.lKey.isPressed)
        {
            int targetFrameRate = -1;
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                targetFrameRate = 10;
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                targetFrameRate = 30;
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                targetFrameRate = 60;
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
                targetFrameRate = 144;
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
                targetFrameRate = 1;
            if (Keyboard.current.digit6Key.wasPressedThisFrame)
                targetFrameRate = 0;

            if (targetFrameRate != -1 && targetFrameRate != 1)
            {
                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = 0;
            }
            else if (targetFrameRate == 1)
            {
                QualitySettings.vSyncCount = 1;
            }
        }
    }
    #endregion
}

[System.Serializable]
public struct CharacterNamePair
{
    public string name;
    public GameObject prefab;
}