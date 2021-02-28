using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    private int currentRebindingIndex;

    private void Start()
    {
        action = GameManager.singleton.input.Gameplay.Get().FindAction(actionName);

        primaryButton.onClick.AddListener(() => OnRemapClicked(0));
        altButton.onClick.AddListener(() => OnRemapClicked(1));

        RefreshButtonText();
    }

    private void RefreshButtonText()
    {
        if (!isRebinding)
        {
            primaryButtonText.text = action.bindings.Count > 0 ? action.GetBindingDisplayString(0) : "N/A";
            altButtonText.text = action.bindings.Count > 1 ? action.GetBindingDisplayString(1) : "N/A";
        }
        else
        {
            if (currentRebindingIndex == 0)
                primaryButtonText.text = "[...]";
            else
                altButtonText.text = "[...]";
        }
    }

    private void OnCompletedBinding(RebindingOperation rebindOp)
    {
        action.Enable();
        rebindOp.Dispose();
        isRebinding = false;

        GamePreferences.Save(new[] { action });

        RefreshButtonText();
    }

    private void OnRemapClicked(int bindingIndex)
    {
        if (isRebinding)
            return;

        while (action.bindings.Count <= bindingIndex)
            action.AddBinding("");

        isRebinding = true;
        currentRebindingIndex = bindingIndex;
        action.Disable();
        action.PerformInteractiveRebinding(bindingIndex)
            .WithExpectedControlType<ButtonControl>()
            //.WithExpectedControlType<AxisControl>()
            .OnComplete(OnCompletedBinding)
            .WithCancelingThrough("<Keyboard>/escape")
            .Start();

        RefreshButtonText();
    }

    private void OnValidate()
    {
        if (labelText)
        {
            labelText.text = labelName;
        }
    }
}
