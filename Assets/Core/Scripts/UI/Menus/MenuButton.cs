using UnityEngine;

/// <summary>
/// Button that can automatically open/close a menu
/// </summary>
public class MenuButton : MonoBehaviour
{
    public enum Function
    {
        Open,
        Close
    }

    public UnityEngine.UI.Button button;

    public Function function;
    public GameObject target;

    private MenuRoot root;

    private void Awake()
    {
        root = GetComponentInParent<MenuRoot>();

        if (button)
        {
            switch (function)
            {
                case Function.Open:
                    button.onClick.AddListener(() => Open(target));
                    break;
                case Function.Close:
                    button.onClick.AddListener(() => Close());
                    break;
            }
        }

        Debug.Assert(root != null);
    }

    private void OnValidate()
    {
        if (button == null)
            button = GetComponent<UnityEngine.UI.Button>();
    }

    public void Close()
    {
        root?.CloseSubmenu();
    }

    public void Open(GameObject menu)
    {
        root?.OpenSubmenu(menu);
    }
}
