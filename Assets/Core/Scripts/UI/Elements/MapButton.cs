using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MapButton : MonoBehaviour
{
    public TextMeshProUGUI nameAndDescription;
    public UnityEngine.UI.Image screenshot;

    public void SetInfo(string inNameAndDescription, Sprite inScreenshot)
    {
        nameAndDescription.text = inNameAndDescription;

        if (inScreenshot != null)
            screenshot.sprite = inScreenshot;
    }
}
