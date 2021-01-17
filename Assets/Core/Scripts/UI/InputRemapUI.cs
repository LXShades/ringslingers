using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;

public class InputRemapUI : MonoBehaviour
{
    public string actionName;
    public string labelName;

    public Button primaryButton;
    public Button altButton;
    public Text primaryButtonText;
    public Text altButtonText;
    public Text labelText;

    private InputAction action;

    private bool isRebinding = false;

    private void Start()
    {
        action = GameManager.singleton.input.Gameplay.Get().FindAction(actionName);

        LoadActionBindings();

        primaryButton.onClick.AddListener(() => OnRemapClicked(0));
        altButton.onClick.AddListener(() => OnRemapClicked(1));
    }

    private void Update()
    {
        primaryButtonText.text = action.bindings.Count > 0 ? action.GetBindingDisplayString(0) : "N/A";
        altButtonText.text = action.bindings.Count > 1 ? action.GetBindingDisplayString(1) : "N/A";
    }

    private void SaveActionBindings()
    {
        string bindings = "";

        for (int i = 0; i < action.bindings.Count; i++)
            bindings += $"{action.bindings[i].effectivePath};";

        PlayerPrefs.SetString($"Control_{action.name}", bindings);
    }

    private void LoadActionBindings()
    {
        string bindingsAsString = PlayerPrefs.GetString($"Control_{action.name}", "");

        action.Disable();
        if (bindingsAsString != "")
        {
            string[] bindings = bindingsAsString.Split(';');

            for (int i = 0; i < bindings.Length; i++)
            {
                if (i < action.bindings.Count)
                    action.ChangeBinding(i).WithPath(bindings[i]);
                else
                    action.AddBinding(bindings[i]);
            }
        }
        action.Enable();
    }

    private void OnCompletedBinding(RebindingOperation rebindOp)
    {
        action.Enable();
        rebindOp.Dispose();
        isRebinding = false;

        SaveActionBindings();
    }

    private void OnRemapClicked(int bindingIndex)
    {
        if (isRebinding)
            return;

        while (action.bindings.Count <= bindingIndex)
            action.AddBinding("");

        isRebinding = true;
        action.Disable();
        action.PerformInteractiveRebinding().OnComplete(OnCompletedBinding).WithTargetBinding(bindingIndex).Start();
    }

    private void OnValidate()
    {
        if (labelText)
        {
            labelText.text = labelName;
        }
    }
}
