using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public MenuRoot menu;

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            GameManager.singleton.isPaused = !GameManager.singleton.isPaused;

        if (GameManager.singleton.isPaused != menu.isOpen)
        {
            if (GameManager.singleton.isPaused)
                menu.Open();
            else
                menu.Close();
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
