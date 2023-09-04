using System;
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

    [System.Flags]
    public enum InputBlockFlags
    {
        Chatbox = 1,
    }

    [Header("Content")]
    [Tooltip("All the content that comes with this build of the game")]
    public RingslingersContentDatabase defaultContent;

    [Header("Level Settings")]
    [Mirror.Scene]
    public string menuScene;

    [Tooltip("How long until items respawn, in seconds")]
    public float itemRespawnTime = 20;

    [Header("Physics")]
    public float gravity = 0.2734375f;

    public float fracunitsPerM = 64;

    public GameObject localObjects;

    public MapConfiguration activeMap
    {
        get => _activeLevel;
        set
        {
            _activeLevel = value;

            // try keep the map rotation in sync (we should probably put this somewhere else)
            foreach (MapRotation mapRotation in RingslingersContent.loaded.mapRotations)
            {
                if (mapRotation.maps.Contains(_activeLevel))
                    activeMapRotation = mapRotation;
            }
        }
    }
    private MapConfiguration _activeLevel;
    public MapRotation activeMapRotation { get; private set; }

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

    // buffered inputs, true if down
    // buffer is kept until PlayerInput.MakeLocalInput() clears them
    public bool bufferedLocalBtnFire;
    public bool bufferedLocalBtnSpin;
    public bool bufferedLocalBtnJump;

    public InputBlockFlags inputBlockFlags { get; private set; } = 0;
    private InputBlockFlags deferredInputBlockFlags = 0; // deferred because update order can cause multiple conflicting things to happen in one frame, often undesirable

    private PlayerCamera cachedCamera = null;

    private string menuSceneName = "";

    private void Awake()
    {
        menuSceneName = System.IO.Path.GetFileNameWithoutExtension(menuScene);

        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        input = new PlayerControls();
        EnableInputs();

        input.Gameplay.Fire.performed += (InputAction.CallbackContext context) => bufferedLocalBtnFire = context.ReadValue<float>() > 0f;
        input.Gameplay.Jump.performed += (InputAction.CallbackContext context) => bufferedLocalBtnJump = context.ReadValue<float>() > 0f;
        input.Gameplay.Spindash.performed += (InputAction.CallbackContext context) => bufferedLocalBtnSpin = context.ReadValue<float>() > 0f;

        RingslingersContent.LoadContent(defaultContent.content);

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
        needsCursor |= SceneManager.GetActiveScene().name == menuSceneName; // main menu needs mouse
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
        if (inputBlockFlags != 0)
            DisableInputs();
        else
            EnableInputs();

        inputBlockFlags = deferredInputBlockFlags;
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

    public void ClearBufferedInputs()
    {
        bufferedLocalBtnFire = false;
        bufferedLocalBtnJump = false;
        bufferedLocalBtnSpin = false;
    }

    private void OnClientDisconnected(Mirror.NetworkConnection conn)
    {
        // go back to the main menu
        SceneManager.LoadSceneAsync(menuScene);
    }

    public void SetInputBlockFlag(InputBlockFlags flag, bool isBlocking)
    {
        if (isBlocking)
            deferredInputBlockFlags |= flag;
        else
            deferredInputBlockFlags &= ~flag;
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