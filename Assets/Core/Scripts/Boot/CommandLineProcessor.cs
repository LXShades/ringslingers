using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommandLineProcessor : MonoBehaviour
{
    public const string kPortParam = "-port";
    public const string kWindowPositionParam = "-pos";
    public const string kOpenConsoleParam = "-console";

    // Start is called before the first frame update
    void Start()
    {
        // Execute scene command line first, we might want to change scene then host
        AsyncOperation op = null;
        if (CommandLine.GetCommand("-scene", 1, out string[] sceneParams))
        {
            op = SetScene(sceneParams[0]);
        }
        else if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            op = SetScene(GameManager.singleton.menuScene);
        }

        if (op != null)
        {
            op.completed += (AsyncOperation operation) => ExecuteCommandLine();
        }
        else
        {
            ExecuteCommandLine();
        }
    }

    private void ExecuteCommandLine()
    {
        /*if (CommandLine.HasCommand("-server"))
        {
            Debug.Log("Running server only!");

            Netplay.singleton.HostServer();
        }*/

        if (CommandLine.GetCommand("-port", 1, out string[] port))
        {
            if (int.TryParse(port[0], out int portInt))
            {
                Debug.Log($"Setting host port to {portInt}");
                NetMan.singleton.transportPort = portInt;
            }
        }

        if (CommandLine.HasCommand("-host"))
        {
            Debug.Log("Hosting!");

            Netplay.singleton.HostServer(null);
        }

        if (CommandLine.GetCommand("-connect", 1, out string[] connectParams))
        {
            Debug.Log($"Connecting to {connectParams[0]}");

            Netplay.singleton.ConnectToServer(connectParams[0]);
        }
    }

    private AsyncOperation SetScene(string scene)
    {
        int sceneIndex;
        bool isIndex = int.TryParse(scene, out sceneIndex);

        Debug.Log($"Loading scene {scene}");

        if (isIndex)
        {
            return SceneManager.LoadSceneAsync(sceneIndex);
        }
        else
        {
            return SceneManager.LoadSceneAsync(scene);
        }
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR // why does !UNITY_EDITOR need to be here, shouldn't standalone be standalone? Oh well, doesn't seem to work that way
        // Window management functions
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern System.IntPtr GetActiveWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(System.IntPtr hwnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(System.IntPtr hwnd, System.IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void PreInit()
        {
            RepositionWindow();
            OpenConsoleIfDedicated();
        }

        private static void RepositionWindow()
        {
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_NOOWNERZORDER = 0x0200;
            const uint SWP_NOREDRAW = 0x0008;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_ASYNCWINDOWPOS = 0x4000;

            int windowX = 0, windowY = 0;

            if (CommandLine.GetCommand(kWindowPositionParam, 2, out string[] posParams))
            {
                int.TryParse(posParams[0], out windowX);
                int.TryParse(posParams[1], out windowY);
            }

            SetForegroundWindow(GetActiveWindow());
            SetWindowPos(GetActiveWindow(), System.IntPtr.Zero, windowX, windowY, 1280, 720, SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOREDRAW | SWP_NOSIZE | SWP_NOZORDER | SWP_ASYNCWINDOWPOS);
        }

        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern bool AllocConsole();

        private static void OpenConsoleIfDedicated()
        {
            if (CommandLine.HasCommand(kOpenConsoleParam))
            {
                AllocConsole();
                System.Console.SetOut(new System.IO.StreamWriter(System.Console.OpenStandardOutput()) { AutoFlush = true });
                Application.logMessageReceivedThreaded += (logString, stackTrace, type) => System.Console.WriteLine(logString);
            }
        }
#endif
}
