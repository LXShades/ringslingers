using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public MenuRoot menu;

    private void Update()
    {
        if (GameManager.singleton.inputBlockFlags == 0 && Keyboard.current.escapeKey.wasPressedThisFrame)
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
}
