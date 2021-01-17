using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public GameObject container;

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
}
