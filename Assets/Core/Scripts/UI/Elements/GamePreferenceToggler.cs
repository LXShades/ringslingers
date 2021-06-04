using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamePreferenceToggler : MonoBehaviour
{
    public enum AffectedPreference
    {
        DebugInfo,
        NetFlowControl
    }

    public Toggle toggle;

    public AffectedPreference affectedPreference;

    private bool preferenceValue
    {
        get
        {
            switch (affectedPreference)
            {
                case AffectedPreference.DebugInfo: return GamePreferences.isDebugInfoEnabled;
                case AffectedPreference.NetFlowControl: return GamePreferences.isNetFlowControlEnabled;
            }
            return false;
        }
        set
        {
            switch (affectedPreference)
            {
                case AffectedPreference.DebugInfo: GamePreferences.isDebugInfoEnabled = value; break;
                case AffectedPreference.NetFlowControl: GamePreferences.isNetFlowControlEnabled = value; break;
            }
        }
    }

    public void OnEnable()
    {
        toggle.isOn = preferenceValue;

        toggle.onValueChanged.AddListener(OnToggled);
    }

    public void OnToggled(bool value)
    {
        preferenceValue = value;
    }

    private void OnValidate()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();
    }
}
