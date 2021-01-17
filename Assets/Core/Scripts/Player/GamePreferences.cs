using System;
using UnityEngine;
using UnityEngine.InputSystem;

public static class GamePreferences
{
    // Mouse speed in degrees per pixel (usually <1!)
    public static float mouseSpeed {
        get => _mouseSpeed;
        set
        {
            _mouseSpeed = value;
            OnPreferencesChanged();
        }
    }

    private static float _mouseSpeed;

    public static event Action onPreferencesChanged;

    // also loads any actions in "actions"
    public static void Load(InputAction[] actions = null)
    {
        _mouseSpeed = PlayerPrefs.GetFloat("MouseSpeed", 1f);

        if (actions != null)
        {
            foreach (InputAction action in actions)
            {
                string bindingsAsString = PlayerPrefs.GetString($"Control_{action.name}", "");

                action.Disable();
                if (bindingsAsString != "")
                {
                    string[] bindings = bindingsAsString.Split(';');

                    for (int i = 0; i < bindings.Length; i++)
                    {
                        if (bindings[i] == "")
                            continue;

                        if (i < action.bindings.Count)
                            action.ChangeBinding(i).WithPath(bindings[i]);
                        else
                            action.AddBinding(bindings[i]);
                    }
                }
                action.Enable();
            }
        }

        onPreferencesChanged?.Invoke();

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
                    bindings += $"{action.bindings[i].effectivePath};";

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
