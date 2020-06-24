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

    public Text debugText;

    void Update()
    {
        Player player = GameManager.singleton.camera.currentPlayer;

        ringsText.text = "0";

        if (player)
        {
            ringsText.text = player.numRings.ToString();

            debugText.text = $"Run Speed: {player.movement.velocity.Horizontal().magnitude.ToString("0.0")}";
        }
    }
}
