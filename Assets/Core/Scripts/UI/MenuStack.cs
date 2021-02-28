using System.Collections.Generic;
using UnityEngine;

public class MenuStack : MonoBehaviour
{
    public bool startActive = false;

    public MenuStack root { get;  private set; }
    public MenuStack parent { get; private set; }
    public List<MenuStack> children { get; private set; } = new List<MenuStack>();

    public List<MenuStack> stack { get; private set; }

    private void Awake()
    {
        if (transform.parent == null || !transform.parent.TryGetComponent(out MenuStack _))
        {
            // this menustack is a root, initialize children
            stack = new List<MenuStack>();
            Initialize(this);
        }

        parent = GetComponentInParent<MenuStack>();
    }

    public void Initialize(MenuStack root)
    {
        this.root = root;

        foreach (Transform child in transform)
        {
            if (child.TryGetComponent(out MenuStack menuStack))
            {
                menuStack.Initialize(root);
                children.Add(menuStack);
            }
        }

        gameObject.SetActive(startActive);
    }

    public void Open(MenuStack target)
    {
        if (root.stack.Count > 0)
            root.stack[root.stack.Count - 1].gameObject.SetActive(false);

        target.gameObject.SetActive(true);
        root.stack.Add(target);
    }

    public void Close()
    {
        if (root.stack.Count > 0)
        {
            root.stack[root.stack.Count - 1].gameObject.SetActive(false);
            root.stack.RemoveAt(root.stack.Count - 1);

            if (root.stack.Count > 0)
                root.stack[root.stack.Count - 1].gameObject.SetActive(true);
        }
    }

    public void CloseAll()
    {
        foreach (MenuStack obj in root.stack)
            obj.gameObject.SetActive(false);

        root.stack.Clear();
    }
}
