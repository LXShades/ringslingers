﻿using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class NetworkEditorTools : MonoBehaviour
{
    /** Stores whether the 'use editor testing' menu checkbox is checked */
    public static bool useEditorTesting
    {
        get { return EditorPrefs.GetBool("netUseEditorTesting"); }
        set { EditorPrefs.SetBool("netUseEditorTesting", value); }
    }

    public static int numTestPlayers
    {
        get { return Mathf.Clamp(EditorPrefs.GetInt("netNumTestPlayers"), 1, 4); }
        set { EditorPrefs.SetInt("netNumTestPlayers", value); }
    }

    public static string buildPath
    {
        get { return $"{Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'))}/NetTest"; }
    }

    public static bool editorIsClient
    {
        get { return EditorPrefs.GetBool("netEditorIsServer"); }
        set { EditorPrefs.SetBool("netEditorIsServer", value); }
    }

    static NetworkEditorTools()
    {
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    [MenuItem("NetTest/Build and Run", priority=1)]
    public static void BuildAndRun()
    {
        List<string> levels = new List<string>();
        string activeScenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        int currentSceneIndex = -1;

        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            levels.Add(EditorBuildSettings.scenes[i].path);

            if (EditorBuildSettings.scenes[i].path == activeScenePath)
            {
                currentSceneIndex = i;
            }
        }

        if (currentSceneIndex == -1)
        {
            currentSceneIndex = EditorBuildSettings.scenes.Length;
            levels.Add(activeScenePath);
        }

        // Build and run the player.. we need to change some build settings though and preserve them
        string originalScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        FullScreenMode originalFullscreenMode = PlayerSettings.fullScreenMode;
        bool originalDefaultIsNativeResolution = PlayerSettings.defaultIsNativeResolution;
        int originalDefaultScreenWidth = PlayerSettings.defaultScreenWidth;
        int originalDefaultScreenHeight = PlayerSettings.defaultScreenHeight;

        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 640;
        PlayerSettings.defaultScreenHeight = 480;

        UnityEditor.Build.Reporting.BuildReport buildReport =
        BuildPipeline.BuildPlayer(levels.ToArray(), $"{buildPath}/build.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);

        PlayerSettings.fullScreenMode = originalFullscreenMode;
        PlayerSettings.defaultIsNativeResolution = originalDefaultIsNativeResolution;
        PlayerSettings.defaultScreenWidth = originalDefaultScreenWidth;
        PlayerSettings.defaultScreenHeight = originalDefaultScreenHeight;

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalScene);

        if (buildReport.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Someone goofed", $"Build failed ({buildReport.summary.totalErrors} errors)", "OK");
            return;
        }
        else
        {
            Run();
        }
    }

    [MenuItem("NetTest/Run", priority = 2)]
    public static void Run()
    {
        if (useEditorTesting)
        {
            // play an instance in the editor
            if (editorIsClient)
            {
                GameManager.editorCommandLineArgs = new string[] { "connect", "127.0.0.1" };
            }

            EditorApplication.isPlaying = true;
        }

        for (int i = useEditorTesting ? 1 : 0; i < numTestPlayers; i++)
        {
            RunBuild();
        }
    }

    private static void OnPlayStateChanged(PlayModeStateChange obj)
    {
        if (obj == PlayModeStateChange.ExitingPlayMode || obj == PlayModeStateChange.ExitingPlayMode)
        {
            GameManager.editorCommandLineArgs = new string[0];
        }
    }

    [MenuItem("NetTest/Use editor for Player 1", priority = 20)]
    private static void UseEditorTesting()
    {
        useEditorTesting = !useEditorTesting;
    }

    [MenuItem("NetTest/Use editor for Player 1", true)]
    private static bool UseEditorTestingValidate()
    {
        Menu.SetChecked("NetTest/Use editor for Player 1", useEditorTesting);
        return true;
    }

    [MenuItem("NetTest/Join as client", priority = 21)]
    private static void EditorIsServer()
    {
        editorIsClient = !editorIsClient;
    }

    [MenuItem("NetTest/Join as client", true)]
    private static bool EditorIsServerValidate()
    {
        Menu.SetChecked("NetTest/Join as client", editorIsClient);
        return useEditorTesting;
    }


    [MenuItem("NetTest/1 player", priority = 40)]
    private static void OneTestPlayer() { numTestPlayers = 1; }

    [MenuItem("NetTest/1 player", true)]
    private static bool OneTestPlayerValidate() { Menu.SetChecked("NetTest/1 player", numTestPlayers == 1); return true; }


    [MenuItem("NetTest/2 players", priority = 41)]
    private static void TwoTestPlayers() { numTestPlayers = 2; }

    [MenuItem("NetTest/2 players", true)]
    private static bool TwoTestPlayersValidate() { Menu.SetChecked("NetTest/2 players", numTestPlayers == 2); return true; }


    [MenuItem("NetTest/3 players", priority = 42)]
    private static void ThreeTestPlayers() { numTestPlayers = 3; }

    [MenuItem("NetTest/3 players", true)]
    private static bool ThreeTestPlayersValidate() { Menu.SetChecked("NetTest/3 players", numTestPlayers == 3); return true; }


    [MenuItem("NetTest/4 players", priority = 43)]
    private static void FourTestPlayers() { numTestPlayers = 4; }

    [MenuItem("NetTest/4 players", true)]
    private static bool FourTestPlayersValidate() { Menu.SetChecked("NetTest/4 players", numTestPlayers == 4); return true; }

    private static void RunBuild()
    {

        // Run another instance of the game
        System.Diagnostics.Process process = new System.Diagnostics.Process();

        process.StartInfo.FileName = $"{buildPath}/build.exe";
        process.StartInfo.WorkingDirectory = buildPath;
        //process.StartInfo.Arguments = $"AutoConnect";

        process.Start();
    }
}

    
#endif