using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EditorBoot
{
    private static bool playModeAutoHost
    {
        get => EditorPrefs.GetBool("_playModeAutoHost", true);
        set => EditorPrefs.SetBool("_playModeAutoHost", value);
    }

    private static string playModeCommandLine
    {
        get => EditorPrefs.GetString("_playModeCommandLineParms", "");
        set => EditorPrefs.SetString("_playModeCommandLineParms", value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnInit()
    {
        // Load the boot scene if necessary
        if (SceneManager.GetActiveScene().buildIndex != 0)
        {
            SceneManager.LoadScene(0, LoadSceneMode.Additive);
        }

        EditorApplication.playModeStateChanged += OnPlayStateChanged;

        // Set default command parameters
        SetDefaultCommands();
    }

    private static void OnPlayStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode)
        {
            Debug.Log("Clearing command line");

            CommandLine.editorCommands = new string[0];
        }
    }

    private static void SetDefaultCommands()
    {
        if (CommandLine.editorCommands.Length == 0 || (CommandLine.editorCommands.Length == 1 && CommandLine.editorCommands[0] == ""))
        {
            Debug.Log($"Setting PlayMode command line: {playModeCommandLine}");
            string[] editorCommands = playModeCommandLine.Split(' ');

            if (playModeAutoHost && SceneManager.GetActiveScene().buildIndex != 0) // boot scene assumes we're not auto hosting
            {
                System.Array.Resize(ref editorCommands, CommandLine.editorCommands.Length + 1);
                editorCommands[CommandLine.editorCommands.Length - 1] = "-host";
            }

            CommandLine.editorCommands = editorCommands;
        }
    }

    private class DefaultCommandLineBox : EditorWindow
    {
        string tempCommands = "";

        [MenuItem("Playtest/Autohost in Playmode", false, 100)]
        static void AutoHostOutsideBoot()
        {
            playModeAutoHost = !playModeAutoHost;
        }

        [MenuItem("Playtest/Autohost in Playmode", validate = true)]
        static bool AutoHostOutsideBootValidate()
        {
            Menu.SetChecked("Playtest/Autohost in Playmode", playModeAutoHost);
            return true;
        }

        [MenuItem("Playtest/Extra PlayMode commands...", false, 101)]
        static void SetDefaultCommands()
        {
            DefaultCommandLineBox window = CreateInstance<DefaultCommandLineBox>();
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 250, 150);
            window.tempCommands = playModeCommandLine;
            window.ShowUtility();
        }

        void OnGUI()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Type commands here!\n-host: Hosts a server with a local player\n-server: Hosts a server only\n-connect [ip]: Connects to the given IP address", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            tempCommands = EditorGUILayout.TextField("Commands:", tempCommands);
            GUILayout.EndHorizontal();
            GUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Done"))
            {
                EditorBoot.playModeCommandLine = tempCommands;
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}