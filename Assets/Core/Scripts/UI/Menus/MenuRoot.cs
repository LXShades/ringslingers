using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The root of a menu stack which can be manipulated with MenuButtons
/// </summary>
public class MenuRoot : MonoBehaviour
{
    public bool openOnAwake = false;

    public List<GameObject> stack = new List<GameObject>();

    public GameObject mainMenu;

    public bool isOpen { get; private set; }

    private void Awake()
    {
        // try to disable all panels
        // since in editor the user might just kinda be like imma leave these open and that'll break everything
        foreach (MenuButton button in GetComponentsInChildren<MenuButton>(true))
        {
            if (button.target)
                button.target.gameObject.SetActive(false);
        }

        // open or close
        if (openOnAwake)
            Open();
        else
            Close();
    }

    public void Open()
    {
        Close();

        OpenSubmenu(mainMenu);
        isOpen = true;
    }

    public void Close()
    {
        foreach (GameObject obj in stack)
            obj.SetActive(false);

        stack.Clear();
        mainMenu.SetActive(false);
        isOpen = false;
    }

    public void OpenSubmenu(GameObject target)
    {
        if (stack.Count > 0)
            stack[stack.Count - 1].SetActive(false);

        target.gameObject.SetActive(true);
        stack.Add(target);

        // todo perhaps: enable parent objects where necessary
        for (Transform transform = target.transform.parent; transform != null; transform = transform.parent)
        {
            transform.gameObject.SetActive(true);

            if (transform.gameObject == this.gameObject)
                break;
        }
    }

    public void CloseSubmenu()
    {
        if (stack.Count > 0)
        {
            stack[stack.Count - 1].SetActive(false);
            stack.RemoveAt(stack.Count - 1);

            if (stack.Count > 0)
                stack[stack.Count - 1].SetActive(true);
            else
                Close(); // last submenu closed mean whole menu is closed
        }
    }
}
