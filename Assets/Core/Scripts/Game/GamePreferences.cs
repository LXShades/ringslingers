using System;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GamePreferences
{
    public const float kDefaultInputSmoothing = 0.017f;
    public const float kDefaultServerRewinding = 0.16f;
    public const float kDefaultOpponentSmoothing = 0.08f;

    // Mouse speed in degrees per pixel (usually <1!)
    public static float mouseSpeed {
        get => _mouseSpeed;
        set
        {
            _mouseSpeed = value;
            OnPreferencesChanged();
        }
    }

    public static float globalVolume
    {
        get => PlayerPrefs.GetFloat("globalVolume", 1f);
        set
        {
            PlayerPrefs.SetFloat("globalVolume", value);
            AudioListener.volume = globalVolume;
            OnPreferencesChanged();
        }
    }

    public static bool isDebugInfoEnabled
    {
        get => PlayerPrefs.GetInt("debugInfo", 0) != 0;
        set 
        {
            PlayerPrefs.SetInt("debugInfo", value ? 1 : 0);
            OnPreferencesChanged();
        }
    }

    public static bool isNetFlowControlEnabled
    {
        get => PlayerPrefs.GetInt("netFlowControl", 1) != 0;
        set
        {
            PlayerPrefs.SetInt("netFlowControl", value ? 1 : 0);
            OnPreferencesChanged();
        }
    }

    public static float inputSmoothing
    {
        get => PlayerPrefs.GetFloat("inputSmoothing", kDefaultInputSmoothing);
        set
        {
            PlayerPrefs.SetFloat("inputSmoothing", value);
            OnPreferencesChanged();
        }
    }

    public static float serverRewindTolerance
    {
        get => PlayerPrefs.GetFloat("serverRewindTolerance", kDefaultServerRewinding);
        set
        {
            PlayerPrefs.SetFloat("serverRewindTolerance", value);
            OnPreferencesChanged();
        }
    }

    public static float opponentSmoothing
    {
        get => PlayerPrefs.GetFloat("opponentSmoothing", kDefaultOpponentSmoothing);
        set
        {
            PlayerPrefs.SetFloat("opponentSmoothing", value);
            OnPreferencesChanged();
        }
    }

    private static float _mouseSpeed;

    public static event Action onPreferencesChanged;

    // also loads any actions in "actions"
    public static void Load(InputAction[] actions = null)
    {
        _mouseSpeed = PlayerPrefs.GetFloat("MouseSpeed", 1f);
        
        AudioListener.volume = globalVolume;

        if (actions != null)
        {
            foreach (InputAction action in actions)
            {
                string bindingsAsString = PlayerPrefs.GetString($"Control_{action.name}", "");

                action.Disable();
                if (bindingsAsString != "")
                {
                    string[] bindings = bindingsAsString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < bindings.Length; i++)
                    {
                        bool isInverted = bindings[i].StartsWith("-");
                        string actualPath = isInverted ? bindings[i].Substring(1) : bindings[i];

                        if (i < action.bindings.Count)
                            action.ChangeBinding(i).WithPath(actualPath);
                        else
                            action.AddBinding(actualPath);

                        InputBinding binding = action.bindings[i];
                        if (isInverted)
                        {
                            binding.processors = "Invert";
                            action.ChangeBinding(i).To(binding);
                        }
                        else
                        {
                            binding.processors = "";
                            action.ChangeBinding(i).To(binding);
                        }
                    }

                    for (int i = bindings.Length; i < action.bindings.Count; i++)
                        action.ChangeBinding(i).Erase();
                }
                action.Enable();
            }
        }

        OnPreferencesChanged();

        Debug.Log("Loaded game preferences");
    }

    // also saves any actions in "actions"
    public static void Save(InputAction[] actions = null)
    {
        PlayerPrefs.SetFloat("MouseSpeed", _mouseSpeed);

        if (actions != null)
        {
            foreach (InputAction action in actions)
            {
                string bindings = "";

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if ((action.bindings[i].effectiveProcessors ?? "").Contains("Invert"))
                        bindings += $"-{action.bindings[i].effectivePath};";
                    else
                        bindings += $"{action.bindings[i].effectivePath};";
                }

                PlayerPrefs.SetString($"Control_{action.name}", bindings);
            }
        }

        Debug.Log("Saved game preferences");
    }

    private static void OnPreferencesChanged()
    {
        onPreferencesChanged?.Invoke();
    }
}
