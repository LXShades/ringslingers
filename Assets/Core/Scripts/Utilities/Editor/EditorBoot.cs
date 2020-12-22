using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EditorBoot
{
    private static string defaultCommandLine
    {
        get => EditorPrefs.GetString("_defaultGameCommandLineParms", "");
        set => EditorPrefs.SetString("_defaultGameCommandLineParms", value);
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
            Debug.Log($"Setting default command line: {defaultCommandLine}");
            CommandLine.editorCommands = defaultCommandLine.Split(' ');
        }
    }

    private class DefaultCommandLineBox : EditorWindow
    {
        string tempCommands = "";

        [MenuItem("Build/Set Play in Editor autocommands...", false, 100)]
        static void SetDefaultCommands()
        {
            DefaultCommandLineBox window = CreateInstance<DefaultCommandLineBox>();
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 250, 150);
            window.tempCommands = EditorBoot.defaultCommandLine;
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
                EditorBoot.defaultCommandLine = tempCommands;
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