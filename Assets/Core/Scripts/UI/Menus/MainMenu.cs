﻿using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public InputField playerName;
    public InputField ipAddress;
    public Button joinButton;
    public Button hostButton;
    public GameObject waitMessage;

    // Start is called before the first frame update
    void Start()
    {
        joinButton.onClick.AddListener(OnJoinClicked);
        hostButton.onClick.AddListener(OnHostClicked);
    }

    private void Update()
    {
        Netplay.singleton.localPlayerIntendedName = playerName.text;
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

        AsyncOperation op = SceneManager.LoadSceneAsync(GameManager.singleton.levelDatabase.levels[0].path);

        op.completed += (AsyncOperation) =>
        {
            Netplay.singleton.HostServer();
        };
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