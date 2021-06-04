using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class DebugGeneralHUD : MonoBehaviour
{
    // debug UI element
    public Text debugText;

    void Update()
    {
        // Debug stuff
        Character player = GameManager.singleton.camera.currentPlayer;

        UpdatePlayerDebugs(player);
    }

    private void UpdatePlayerDebugs(Character player)
    {
        debugText.text =
            $"{Netplay.singleton.netStat}\n";

        if (player)
        {
            // Movement
            debugText.text +=
                $"Player \"{player.playerName}\" info ===\n" +
                $"Velocity: {player.movement.velocity} ({player.movement.velocity.magnitude:F2})\n" +
                $"Ground: {player.movement.isOnGround}\n" +
                $"GroundNml: {player.movement.groundNormal}\n" +
                $"GroundVel: {player.movement.groundVelocity}\n" +
                $"Up: {player.movement.up}\n" +
                $"RunVel: {player.movement.runVelocity}\n\n";
        }

        // debug stuff for other players in the same scene
        if (GameTicker.singleton)
            debugText.text += $"Ticker info: ===\n{GameTicker.singleton.DebugInfo()}\n";

        // net smoothing debug
        if (Mirror.NetworkClient.active)
        {
            debugText.text +=
                $"Net flow control: ===\n" +
                $"Enabled: {NetworkClient.connection.isFlowControlled}\n" +
                $"{(NetworkClient.connection.isFlowControlled ? NetworkClient.connection.flowController.ToString() : "")}\n";
        }
    }
}
