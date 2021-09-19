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
    [Mirror.Scene]
    public string menuScene;

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

    public bool canPlayInputs { get; private set; } = true;
    public bool canPlayMouselook { get; private set; } = true;
    public bool canPlayWeaponFire { get; private set; } = true;

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
    }

    private void Start()
    {
        NetMan.singleton.onClientDisconnect += OnClientDisconnected;
    }

    void Update()
    {
        bool needsCursor = false;
        bool isWeaponWheelOpen = input.Gameplay.WeaponWheel.ReadValue<float>() > 0.5f;

        needsCursor |= isPaused; // pause menu needs mouse
        needsCursor |= string.Compare(SceneManager.GetActiveScene().path, menuScene, true) == 0; // main menu needs mouse
        needsCursor |= isWeaponWheelOpen; // weapon wheel needs mouse

        CursorLockMode lockMode = needsCursor ? CursorLockMode.None : CursorLockMode.Locked;

        if (Cursor.lockState != lockMode) // is it a lock state or lock mode, who knows
            Cursor.lockState = lockMode;

        canPlayInputs = !isPaused;
        canPlayMouselook = canPlayWeaponFire = !needsCursor;

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
        SceneManager.LoadSceneAsync(menuScene);
    }

    public void SuppressGameplayInputs()
    {
        doSuppressGameplayInputs = true;
    }

#if UNITY_EDITOR
    [UnityEditor.Callbacks.DidReloadScripts]
    static void OnScriptsRecompiled()
    {
        if (Application.isPlaying)
        {
            singleton.input = new PlayerControls();
            singleton.areInputsEnabled = false;
            GamePreferences.Load(singleton.input.asset.FindActionMap("Gameplay").actions.ToArray());
        }
    }
#endif

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