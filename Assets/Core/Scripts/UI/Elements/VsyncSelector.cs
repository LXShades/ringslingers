using UnityEngine;
using UnityEngine.UI;

public class VsyncSelector : MonoBehaviour
{
    public Toggle toggle;

    private void OnEnable()
    {
        toggle.isOn = (QualitySettings.vSyncCount == 1);
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }
    private void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void Update()
    {
        if (toggle.isOn != (QualitySettings.vSyncCount == 1))
            toggle.SetIsOnWithoutNotify(QualitySettings.vSyncCount == 1);
    }

    private void OnToggleChanged(bool val)
    {
        QualitySettings.vSyncCount = val ? 1 : 0;
    }
}
