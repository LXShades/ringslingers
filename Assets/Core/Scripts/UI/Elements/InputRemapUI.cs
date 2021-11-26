using System.Collections.Generic;
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
    private int currentRebindingIndex;
    private RebindingOperation currentRebindingOp;

    private static Dictionary<InputControl, float> lastKnownInputValues = new Dictionary<InputControl, float>();

    private void Start()
    {
        action = GameManager.singleton.input.Gameplay.Get().FindAction(actionName);

        primaryButton.onClick.AddListener(() => OnRemapClicked(0));
        altButton.onClick.AddListener(() => OnRemapClicked(1));

        RefreshButtonText();
    }

    private void OnDisable()
    {
        if (isRebinding)
        {
            if (currentRebindingOp != null)
            {
                currentRebindingOp.Cancel();
            }
        }
    }

    private void RefreshButtonText()
    {
        if (!isRebinding)
        {
            primaryButtonText.text = action.bindings.Count > 0 ? $"{((action.bindings[0].effectiveProcessors ?? "").Contains("Invert") ? "-" : "")}{action.GetBindingDisplayString(0)}" : "N/A";
            altButtonText.text = action.bindings.Count > 1 ? $"{((action.bindings[1].effectiveProcessors ?? "").Contains("Invert") ? "-" : "")}{action.GetBindingDisplayString(1)}" : "N/A";
        }
        else
        {
            if (currentRebindingIndex == 0)
                primaryButtonText.text = "[...]\n(DEL to clear)";
            else
                altButtonText.text = "[...]\n(DEL to clear)";
        }
    }

    private void OnCompletedBinding(RebindingOperation rebindOp)
    {
        if (rebindOp.selectedControl.path == "/Keyboard/delete")
            action.ChangeBinding(currentRebindingIndex).Erase(); // DELETE clears the binding
        else
        {
            // Invert if inverted control was requested
            if (lastKnownInputValues.ContainsKey(rebindOp.selectedControl))
            {
                float value = lastKnownInputValues[rebindOp.selectedControl];
                InputBinding binding = action.bindings[currentRebindingIndex];

                if (value > 0f)
                    binding.processors = "";
                else if (value < 0f)
                    binding.processors = "Invert";

                action.ChangeBinding(currentRebindingIndex).To(binding);
            }
        }

        action.Enable();
        rebindOp.Dispose();

        isRebinding = false;
        GamePreferences.Save(new[] { action });

        RefreshButtonText();
    }

    private void OnPotentialBindingMatch(RebindingOperation rebindOp)
    {
        // we don't support composite Vector2 controls currently
        for (int i = 0; i < rebindOp.candidates.Count; i++)
        {
            if (rebindOp.candidates[i].valueType == typeof(Vector2))
                rebindOp.RemoveCandidate(rebindOp.candidates[i--]);
        }
    }

    private float OnComputeScore(InputControl inputControl, UnityEngine.InputSystem.LowLevel.InputEventPtr eventPtr)
    {
        unsafe
        {
            object val = inputControl.ReadValueFromStateAsObject(inputControl.GetStatePtrFromStateEvent(eventPtr));

            if (val is float)
                lastKnownInputValues[inputControl] = (float)val;
            return inputControl.EvaluateMagnitude(inputControl.GetStatePtrFromStateEvent(eventPtr)) + (inputControl.synthetic ? 0f : 1f);
        }
    }

    private void OnCancel(RebindingOperation rebindOp)
    {
        isRebinding = false;

        action.Enable();

        currentRebindingOp.Dispose();
        currentRebindingOp = null;

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
        currentRebindingOp = action.PerformInteractiveRebinding(bindingIndex)
            .OnComplete(OnCompletedBinding)
            .OnPotentialMatch(OnPotentialBindingMatch)
            .OnComputeScore(OnComputeScore)
            .OnCancel(OnCancel)
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
