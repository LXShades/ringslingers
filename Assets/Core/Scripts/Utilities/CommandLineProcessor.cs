using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CommandLineProcessor : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
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

        if (CommandLine.HasCommand("-host"))
        {
            Debug.Log("Hosting!");

            Netplay.singleton.HostServer();
        }

        if (CommandLine.GetCommand("-connect", 1, out string[] connectParams))
        {
            Debug.Log($"Connecting to {connectParams[0]}");

            Netplay.singleton.ConnectToServer(connectParams[0]);
        }

        if (CommandLine.GetCommand("-scene", 1, out string[] sceneParams))
        {
            Debug.Log($"Setting scene to {sceneParams[0]}");

            if (NetworkServer.active)
                NetMan.singleton.ServerChangeScene(sceneParams[0]);
            else if (!NetworkClient.active)
                SceneManager.LoadScene(sceneParams[0]);
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
}
