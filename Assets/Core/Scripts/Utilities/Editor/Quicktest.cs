using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class NetworkEditorTools : MonoBehaviour
{
    private enum EditorRole
    {
        None = 0,
        Host = 1,
        Server = 2,
        Client = 3
    }

    public static int numTestPlayers
    {
        get => Mathf.Clamp(EditorPrefs.GetInt("netNumTestPlayers"), 1, 4);
        set => EditorPrefs.SetInt("netNumTestPlayers", value);
    }

    public static bool onlyBuildCurrentScene
    {
        get => EditorPrefs.GetBool("netOnlyBuildCurrentScene", false);
        set => EditorPrefs.SetBool("netOnlyBuildCurrentScene", value);
    }

    public static string buildPath => $"{Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'))}/Builds/QuickTest/{Application.productName}";

    public static string webGlBuildPath => $"{Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'))}/Builds/WebGL/{Application.productName}";
    public static string linuxBuildPath => $"{Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'))}/Builds/Linux/{Application.productName}";

    private static EditorRole editorRole
    {
        get { return (EditorRole)EditorPrefs.GetInt("editorRole"); }
        set { EditorPrefs.SetInt("editorRole", (int)value); }
    }

    [MenuItem("Playtest/Build", priority = 1)]
    public static bool Build()
    {
        // Add the open scene to the list if it's not already in there
        List<string> levels = new List<string>();
        string activeScenePath = EditorSceneManager.GetActiveScene().path;
        int currentSceneIndex = -1;

        if (onlyBuildCurrentScene)
        {
            if (EditorBuildSettings.scenes.Length > 0)
                levels.Add(EditorBuildSettings.scenes[0].path);

            levels.Add(activeScenePath);
            currentSceneIndex = 1;
        }
        else
        {
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                {
                    levels.Add(EditorBuildSettings.scenes[i].path);

                    if (EditorBuildSettings.scenes[i].path == activeScenePath)
                    {
                        currentSceneIndex = i;
                    }
                }
            }

            if (currentSceneIndex == -1)
            {
                levels.Add(activeScenePath);
            }
        }

        // Build and run the player, preserving the open scene
        string originalScene = EditorSceneManager.GetActiveScene().path;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            string buildName = $"{buildPath}/{Application.productName}.exe";
            UnityEditor.Build.Reporting.BuildReport buildReport = BuildPipeline.BuildPlayer(levels.ToArray(), buildName, BuildTarget.StandaloneWindows64, BuildOptions.Development);

            EditorSceneManager.OpenScene(originalScene);

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
        else
        {
            return false;
        }
    }

    [MenuItem("Playtest/Build && Run", priority = 20)]
    public static void BuildAndRun()
    {
        if (Build())
            Run();
    }


    [MenuItem("Playtest/Run", priority = 21)]
    public static void Run()
    {
        string dimensions = $"-screen-fullscreen 0 -screen-width {Screen.currentResolution.width / 2} -screen-height {Screen.currentResolution.height / 2}";

        switch (editorRole)
        {
            case EditorRole.Client:
                CommandLine.editorCommands = new string[] { "-connect", "127.0.0.1" };
                RunBuild($"-host {dimensions} -scene {EditorSceneManager.GetActiveScene().path}");
                break;
            case EditorRole.Server:
                CommandLine.editorCommands = new string[] { "-host", "127.0.0.1", "-scene", EditorSceneManager.GetActiveScene().path };
                RunBuild($"-connect 127.0.0.1 {dimensions}");
                break;
            case EditorRole.Host:
                CommandLine.editorCommands = new string[] { "-host", "127.0.0.1", "-scene", EditorSceneManager.GetActiveScene().path };
                break;
            case EditorRole.None:
                RunBuild($"-host {dimensions} -scene {EditorSceneManager.GetActiveScene().path}");
                RunBuild($"-connect 127.0.0.1 {dimensions}");
                break;
        }

        // Connect the remaining players
        for (int i = 0; i < numTestPlayers - 1; i++)
        {
            RunBuild($"-connect 127.0.0.1 {dimensions}");
        }

        if (editorRole != EditorRole.None)
        {
            EditorApplication.isPlaying = true;
        }
    }


    [MenuItem("Playtest/Final/Server Build")]
    public static void BuildFinalServer()
    {
        string originalScene = EditorSceneManager.GetActiveScene().path;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        UnityEditor.Build.Reporting.BuildReport buildReport = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, $"{linuxBuildPath}/build.x86_64", BuildTarget.StandaloneLinux64, BuildOptions.EnableHeadlessMode);

        EditorSceneManager.OpenScene(originalScene);

        if (buildReport.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Someone goofed", $"Build failed ({buildReport.summary.totalErrors} errors)", "OK");
        }
    }

    [MenuItem("Playtest/Final/WebGL Build")]
    public static void BuildFinalWebGL()
    {
        string originalScene = EditorSceneManager.GetActiveScene().path;

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        UnityEditor.Build.Reporting.BuildReport buildReport = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, $"{webGlBuildPath}/", BuildTarget.WebGL, BuildOptions.None);

        EditorSceneManager.OpenScene(originalScene);

        if (buildReport.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Someone goofed", $"Build failed ({buildReport.summary.totalErrors} errors)", "OK");
        }
    }

    [MenuItem("Playtest/Standalone Only", priority = 40)]
    private static void StandaloneOnly() { editorRole = EditorRole.None; }

    [MenuItem("Playtest/Standalone Only", true)]
    private static bool StandaloneOnlyValidate() { Menu.SetChecked("Playtest/Standalone Only", editorRole == EditorRole.None); return true; }

    [MenuItem("Playtest/Editor is Host", priority = 41)]
    private static void EditorIsHost() { editorRole = EditorRole.Host; }

    [MenuItem("Playtest/Editor is Host", true)]
    private static bool EditorIsHostValidate() { Menu.SetChecked("Playtest/Editor is Host", editorRole == EditorRole.Host); return true; }

    [MenuItem("Playtest/Editor is Server", priority = 42)]
    private static void EditorIsServer() { editorRole = EditorRole.Server; }

    [MenuItem("Playtest/Editor is Server", true)]
    private static bool EditorIsServerValidate() { Menu.SetChecked("Playtest/Editor is Server", editorRole == EditorRole.Server); return true; }

    [MenuItem("Playtest/Editor is Client", priority = 43)]
    private static void EditorIsClient() { editorRole = EditorRole.Client; }

    [MenuItem("Playtest/Editor is Client", true)]
    private static bool EditorIsClientValidate() { Menu.SetChecked("Playtest/Editor is Client", editorRole == EditorRole.Client); return true; }

    [MenuItem("Playtest/1 player", priority = 80)]
    private static void OneTestPlayer() { numTestPlayers = 1; }

    [MenuItem("Playtest/1 player", true)]
    private static bool OneTestPlayerValidate() { Menu.SetChecked("Playtest/1 player", numTestPlayers == 1); return true; }


    [MenuItem("Playtest/2 players", priority = 81)]
    private static void TwoTestPlayers() { numTestPlayers = 2; }

    [MenuItem("Playtest/2 players", true)]
    private static bool TwoTestPlayersValidate() { Menu.SetChecked("Playtest/2 players", numTestPlayers == 2); return true; }


    [MenuItem("Playtest/3 players", priority = 82)]
    private static void ThreeTestPlayers() { numTestPlayers = 3; }

    [MenuItem("Playtest/3 players", true)]
    private static bool ThreeTestPlayersValidate() { Menu.SetChecked("Playtest/3 players", numTestPlayers == 3); return true; }


    [MenuItem("Playtest/4 players", priority = 83)]
    private static void FourTestPlayers() { numTestPlayers = 4; }

    [MenuItem("Playtest/4 players", true)]
    private static bool FourTestPlayersValidate() { Menu.SetChecked("Playtest/4 players", numTestPlayers == 4); return true; }


    [MenuItem("Playtest/Only build current scene", priority = 120)]
    private static void OnlyCurrentScene() { onlyBuildCurrentScene = !onlyBuildCurrentScene; }

    [MenuItem("Playtest/Only build current scene", true)]
    private static bool OnlyCurrentSceneValidate() { Menu.SetChecked("Playtest/Only build current scene", onlyBuildCurrentScene); return true; }

    private static void RunBuild(string arguments = "")
    {
        // Run another instance of the game
        System.Diagnostics.Process process = new System.Diagnostics.Process();

        process.StartInfo.FileName = $"{buildPath}/{Application.productName}.exe";
        process.StartInfo.WorkingDirectory = buildPath;
        process.StartInfo.Arguments = arguments;

        process.Start();
    }
}