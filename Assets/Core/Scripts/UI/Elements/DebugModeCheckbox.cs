using UnityEngine;
using UnityEngine.UI;

public class DebugModeCheckbox : MonoBehaviour
{
    public Toggle checkbox;

    private void Awake()
    {
        checkbox.onValueChanged.AddListener(OnChanged);
    }

    private void OnEnable()
    {
        checkbox.isOn = GamePreferences.isDebugInfoEnabled;
    }

    private void OnChanged(bool value)
    {
        GamePreferences.isDebugInfoEnabled = value;
    }

    private void OnValidate()
    {
        if (checkbox == null)
            checkbox = GetComponent<Toggle>();
    }
}
