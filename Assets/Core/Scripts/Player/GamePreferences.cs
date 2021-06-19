﻿using System;
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

    public static float minClientDelayMs
    {
        get => PlayerPrefs.GetFloat("minClientDelay", 16f);
        set
        {
            PlayerPrefs.SetFloat("minClientDelay", value);
            OnPreferencesChanged();
        }
    }

    public static float maxClientDelayMs
    {
        get => PlayerPrefs.GetFloat("maxClientDelay", 50f);
        set
        {
            PlayerPrefs.SetFloat("maxClientDelay", value);
            OnPreferencesChanged();
        }
    }

    public static float minServerDelayMs
    {
        get => PlayerPrefs.GetFloat("minServerDelay", 16f);
        set
        {
            PlayerPrefs.SetFloat("minServerDelay", value);
            OnPreferencesChanged();
        }
    }

    public static float maxServerDelayMs
    {
        get => PlayerPrefs.GetFloat("maxServerDelay", 50f);
        set
        {
            PlayerPrefs.SetFloat("maxServerDelay", value);
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
