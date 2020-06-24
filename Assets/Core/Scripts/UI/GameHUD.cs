using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game HUD. Since this is purely visual, we can use a regular MonoBehaviour
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Hierarchy")]
    public Text ringsText;

    void Update()
    {
        Player player = GameManager.singleton.camera.currentPlayer;

        ringsText.text = "0";

        if (player)
        {
            ringsText.text = player.numRings.ToString();
        }
    }
}
