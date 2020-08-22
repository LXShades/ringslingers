using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class NetworkEditorTools : MonoBehaviour
{
    public static int numTestPlayers
    {
        get { return Mathf.Clamp(EditorPrefs.GetInt("netNumTestPlayers"), 1, 4); }
        set { EditorPrefs.SetInt("netNumTestPlayers", value); }
    }

    public static string buildPath
    {
        get { return $"{Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'))}/NetTest"; }
    }

    static NetworkEditorTools()
    {
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    [MenuItem("NetTest/Build", priority=1)]
    public static bool Build()
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
            return false;
        }
        else
        {
            return true;
        }
    }


    [MenuItem("NetTest/Run", priority = 2)]
    public static void Run()
    {
        // Run the builds
        for (int i = 0; i < numTestPlayers; i++)
        {
            if (i == 0)
                RunBuild("server");
            else
                RunBuild("connect 127.0.0.1");
        }
    }

    [MenuItem("NetTest/Build && Run as Server", priority = 20)]
    public static void BuildAndRunServer()
    {
        if (Build())
            RunAsServer();
    }

    [MenuItem("NetTest/Run as Server", priority = 21)]
    private static void RunAsServer()
    {
        GameManager.editorCommandLineArgs = new string[] { "server", "127.0.0.1" };
        EditorApplication.isPlaying = true;

        // Run the builds
        for (int i = 0; i < numTestPlayers - 1; i++)
        {
            RunBuild("connect 127.0.0.1");
        }
    }

    [MenuItem("NetTest/Build && Run as Client", priority = 40)]
    public static void BuildAndRunClient()
    {
        if (Build())
            RunAsClient();
    }

    [MenuItem("NetTest/Run as Client", priority = 41)]
    private static void RunAsClient()
    {
        GameManager.editorCommandLineArgs = new string[] { "connect", "127.0.0.1" };
        EditorApplication.isPlaying = true;

        for (int i = 0; i < Mathf.Max(numTestPlayers - 1, 1); i++)
        {
            if (i == 0)
                RunBuild("server");
            else
                RunBuild("connect 127.0.0.1");
        }
    }

    [MenuItem("NetTest/1 player", priority = 80)]
    private static void OneTestPlayer() { numTestPlayers = 1; }

    [MenuItem("NetTest/1 player", true)]
    private static bool OneTestPlayerValidate() { Menu.SetChecked("NetTest/1 player", numTestPlayers == 1); return true; }


    [MenuItem("NetTest/2 players", priority = 81)]
    private static void TwoTestPlayers() { numTestPlayers = 2; }

    [MenuItem("NetTest/2 players", true)]
    private static bool TwoTestPlayersValidate() { Menu.SetChecked("NetTest/2 players", numTestPlayers == 2); return true; }


    [MenuItem("NetTest/3 players", priority = 82)]
    private static void ThreeTestPlayers() { numTestPlayers = 3; }

    [MenuItem("NetTest/3 players", true)]
    private static bool ThreeTestPlayersValidate() { Menu.SetChecked("NetTest/3 players", numTestPlayers == 3); return true; }


    [MenuItem("NetTest/4 players", priority = 83)]
    private static void FourTestPlayers() { numTestPlayers = 4; }

    [MenuItem("NetTest/4 players", true)]
    private static bool FourTestPlayersValidate() { Menu.SetChecked("NetTest/4 players", numTestPlayers == 4); return true; }

    private static void OnPlayStateChanged(PlayModeStateChange obj)
    {
        if (obj == PlayModeStateChange.ExitingPlayMode || obj == PlayModeStateChange.ExitingPlayMode)
        {
            GameManager.editorCommandLineArgs = new string[0];
        }
    }

    private static void RunBuild(string arguments = "")
    {

        // Run another instance of the game
        System.Diagnostics.Process process = new System.Diagnostics.Process();

        process.StartInfo.FileName = $"{buildPath}/build.exe";
        process.StartInfo.WorkingDirectory = buildPath;
        process.StartInfo.Arguments = arguments;

        process.Start();
    }
}

    
#endif