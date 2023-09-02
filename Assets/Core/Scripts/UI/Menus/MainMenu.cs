using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public InputField playerName;
    public InputField ipAddress;
    public Button joinButton;
    public Button hostButton;
    public Button quitButton;
    public GameObject waitMessage;

    // Start is called before the first frame update
    void Start()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
        hostButton.onClick.AddListener(OnHostClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        playerName.onValueChanged.AddListener(OnNameChanged);

        OnNameChanged(playerName.text);
    }

    private void OnJoinClicked()
    {
        SetMenuEnabled(false);

        NetMan.singleton.onClientDisconnect -= OnConnectionFailed;
        NetMan.singleton.onClientDisconnect += OnConnectionFailed;
        Netplay.singleton.ConnectToServer(ipAddress.text);
    }

    private void OnHostClicked()
    {
        SetMenuEnabled(false);

        AsyncOperation op = SceneManager.LoadSceneAsync(RingslingersContent.loaded.levels[0].path);

        op.completed += (AsyncOperation) =>
        {
            Netplay.singleton.HostServer();
        };
    }

    private void OnQuitClicked()
    {
        Application.Quit();
    }

    private void OnNameChanged(string newName)
    {
        LocalPersistentPlayer persistent = Player.localPersistent;

        persistent.name = newName;
        Player.localPersistent = persistent;
    }

    private void SetMenuEnabled(bool enabled)
    {
        joinButton.interactable = enabled;
        hostButton.interactable = enabled;
        waitMessage.SetActive(!enabled);
    }

    private void OnConnectionFailed(Mirror.NetworkConnection conn)
    {
        SetMenuEnabled(true);
    }
}
