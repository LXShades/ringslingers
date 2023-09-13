using System.Linq;
using TMPro;
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
    public GameObject messageBoxPanel;
    public TextMeshProUGUI messageBoxText;

    // Start is called before the first frame update
    void Start()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        playerName.onValueChanged.AddListener(OnNameChanged);

        if (Netplay.singleton && !string.IsNullOrEmpty(Netplay.singleton.nextDisconnectionErrorMessage))
        {
            messageBoxPanel.SetActive(true);
            messageBoxText.text = Netplay.singleton.nextDisconnectionErrorMessage;
            Netplay.singleton.ClearDisconnectionErrorMessage();
        }
        else
        {
            messageBoxPanel.SetActive(false);
        }

        OnNameChanged(playerName.text);
    }

    private void OnDestroy()
    {
        NetMan.singleton.onClientDisconnect -= OnConnectionFailed;
    }

    private void OnJoinClicked()
    {
        SetMenuEnabled(false);

        NetMan.singleton.onClientDisconnect -= OnConnectionFailed;
        NetMan.singleton.onClientDisconnect += OnConnectionFailed;
        Netplay.singleton.ConnectToServer(ipAddress.text);
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
