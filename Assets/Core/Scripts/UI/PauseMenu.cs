using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject container;

    public Dropdown resolutions;

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            GameManager.singleton.isPaused = !GameManager.singleton.isPaused;

        container.SetActive(GameManager.singleton.isPaused);
    }

    public void Close()
    {
        GameManager.singleton.isPaused = false;
    }

    public void SetFullscreenMode(int index)
    {
        switch (index)
        {
            case 0: Screen.fullScreenMode = FullScreenMode.Windowed; break;
            case 1: Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
            case 2: Screen.fullScreenMode = FullScreenMode.FullScreenWindow; break;
        }
    }

    public void GoNextLevel()
    {
        Netplay.singleton.ServerNextMap();
    }
}
