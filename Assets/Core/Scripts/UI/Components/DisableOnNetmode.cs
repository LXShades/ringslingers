using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableOnNetmode : MonoBehaviour
{
    public Mirror.NetworkManagerMode disableOnMode;

    private void Update()
    {
        bool shouldEnable = NetMan.singleton == null || disableOnMode != NetMan.singleton.mode;
        
        foreach (UnityEngine.UI.Selectable selectable in GetComponents<UnityEngine.UI.Selectable>())
            selectable.interactable = shouldEnable;
    }
}
