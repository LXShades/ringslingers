using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class NetworkEditorTools : MonoBehaviour
{
    private enum EditorRole
    {
        None = 0,
        Server = 1,
        Client = 2
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

    private static EditorRole editorRole
    {
        get { return (EditorRole)EditorPrefs.GetInt("editorRole"); }
        set { EditorPrefs.SetInt("editorRole", (int)value); }
    }

    static NetworkEditorTools()
    {
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    [MenuItem("Build/Build", priority = 1)]
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

    [MenuItem("Build/Build && Run", priority = 20)]
    public static void BuildAndRun()
    {
        if (Build())
            Run();
    }


    [MenuItem("Build/Run", priority = 21)]
    public static void Run()
    {
        switch (editorRole)
        {
            case EditorRole.Client:
                GameManager.editorCommandLineArgs = new string[] { "connect", "127.0.0.1" };
                EditorApplication.isPlaying = true;
                break;
            case EditorRole.Server:
                GameManager.editorCommandLineArgs = new string[] { "server", "127.0.0.1" };
                EditorApplication.isPlaying = true;
                break;
        }

        // Run the builds
        for (int i = 0; i < (editorRole == EditorRole.None ? numTestPlayers : numTestPlayers - 1); i++)
        {
            if (i == 0 && editorRole != EditorRole.Server) // run the server
                RunBuild("server");
            else
                RunBuild("connect 127.0.0.1");
        }
    }

    [MenuItem("Build/Standalone Only", priority = 40)]
    private static void StandaloneOnly() { editorRole = EditorRole.None; }

    [MenuItem("Build/Standalone Only", true)]
    private static bool StandaloneOnlyValidate() { Menu.SetChecked("Build/Standalone Only", editorRole == EditorRole.None); return true; }

    [MenuItem("Build/Editor is Server", priority = 41)]
    private static void EditorIsServer() { editorRole = EditorRole.Server; }

    [MenuItem("Build/Editor is Server", true)]
    private static bool EditorIsServerValidate() { Menu.SetChecked("Build/Editor is Server", editorRole == EditorRole.Server); return true; }

    [MenuItem("Build/Editor is Client", priority = 42)]
    private static void EditorIsClient() { editorRole = EditorRole.Client; }

    [MenuItem("Build/Editor is Client", true)]
    private static bool EditorIsClientValidate() { Menu.SetChecked("Build/Editor is Client", editorRole == EditorRole.Client); return true; }

    [MenuItem("Build/1 player", priority = 80)]
    private static void OneTestPlayer() { numTestPlayers = 1; }

    [MenuItem("Build/1 player", true)]
    private static bool OneTestPlayerValidate() { Menu.SetChecked("Build/1 player", numTestPlayers == 1); return true; }


    [MenuItem("Build/2 players", priority = 81)]
    private static void TwoTestPlayers() { numTestPlayers = 2; }

    [MenuItem("Build/2 players", true)]
    private static bool TwoTestPlayersValidate() { Menu.SetChecked("Build/2 players", numTestPlayers == 2); return true; }


    [MenuItem("Build/3 players", priority = 82)]
    private static void ThreeTestPlayers() { numTestPlayers = 3; }

    [MenuItem("Build/3 players", true)]
    private static bool ThreeTestPlayersValidate() { Menu.SetChecked("Build/3 players", numTestPlayers == 3); return true; }


    [MenuItem("Build/4 players", priority = 83)]
    private static void FourTestPlayers() { numTestPlayers = 4; }

    [MenuItem("Build/4 players", true)]
    private static bool FourTestPlayersValidate() { Menu.SetChecked("Build/4 players", numTestPlayers == 4); return true; }

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