﻿using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommandLineProcessor : MonoBehaviour
{

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
            op = SetScene("1");
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

        int hostPort = -1;
        if (CommandLine.GetCommand("-port", 1, out string[] port))
        {
            if (!int.TryParse(port[0], out hostPort))
                hostPort = -1;
        }

        if (CommandLine.HasCommand("-host"))
        {
            Debug.Log("Hosting!");

            Netplay.singleton.HostServer(hostPort);
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
        bool isInt = int.TryParse(scene, out sceneIndex);

        Debug.Log($"Loading scene {scene}");

        if (isInt)
        {
            return SceneManager.LoadSceneAsync(sceneIndex);
        }
        else
        {
            return SceneManager.LoadSceneAsync(scene);
        }
    }

    // Window management functions
    [DllImport("user32.dll")]
    private static extern System.IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(System.IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(System.IntPtr hwnd, System.IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void PreInitWindowPosition()
    {
        Debug.Log("Trying to reposition game window");
        //Display.onDisplaysUpdated += RepositionWindow;
        //Application.focusChanged += (bool _) => RepositionWindow();

        RepositionWindow();
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

        if (CommandLine.GetCommand("-pos", 2, out string[] posParams))
        {
            System.Int32.TryParse(posParams[0], out windowX);
            System.Int32.TryParse(posParams[1], out windowY);
        }

        SetForegroundWindow(GetActiveWindow());
        SetWindowPos(GetActiveWindow(), System.IntPtr.Zero, windowX, windowY, 1280, 720, SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOREDRAW | SWP_NOSIZE | SWP_NOZORDER | SWP_ASYNCWINDOWPOS);
    }

    /*
    [DllImport("user32.dll")]
    private static extern System.IntPtr GetWindowThreadProcessId(System.IntPtr hwnd, out uint pid);
        System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();

        GetWindowThreadProcessId(GetActiveWindow(), out uint activeProcId);

        if (activeProcId == proc.Id)
            Display_onDisplaysUpdated();
     * */
}