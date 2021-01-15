using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public InputField playerName;
    public InputField ipAddress;
    public Button joinButton;
    public Button hostButton;
    public string defaultHostScene;

    // Start is called before the first frame update
    void Start()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
        hostButton.onClick.AddListener(OnHostClicked);
    }

    private void OnJoinClicked()
    {
        Netplay.singleton.ConnectToServer(ipAddress.text);
    }

    private void OnHostClicked()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(defaultHostScene);

        op.completed += (AsyncOperation) =>
        {
            Netplay.singleton.HostServer();
        };
    }
}
