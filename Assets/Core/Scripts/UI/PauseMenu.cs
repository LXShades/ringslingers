using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public GameObject container;
    public MenuStack stackRoot;
    public MenuStack mainPanel;

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            GameManager.singleton.isPaused = !GameManager.singleton.isPaused;

        if (GameManager.singleton.isPaused != container.activeSelf)
        {
            if (GameManager.singleton.isPaused)
            {
                container.SetActive(true);
                stackRoot.Open(mainPanel);
            }
            else
            {
                stackRoot.CloseAll();
                container.SetActive(false);
            }
        }
    }

    public void Close()
    {
        GameManager.singleton.isPaused = false;
    }

    public void GoNextLevel()
    {
        Netplay.singleton.ServerNextMap();
    }
}
