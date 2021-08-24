using Mirror;
using UnityEngine;
using UnityEngine.UI;

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
        if (NetworkServer.active)
        {
            debugText.text += $"Server net flow control: ===\n";
            foreach (Character character in Netplay.singleton.players)
            {
                if (character && character.connectionToClient != NetworkServer.localConnection)
                {
                    NetworkConnectionToClient clientConnection = character.connectionToClient as NetworkConnectionToClient;
                    debugText.text += $"{character.playerName}: {(clientConnection != null ? clientConnection.unbatcher.flowController.ToString() : "[flow control disabled]")}\n";
                }
            }
        }
        else if (NetworkClient.isConnected)
        {
            debugText.text +=
                $"Client net flow control: ===\n" +
                $"Local: {(NetworkClient.unbatcher.enableFlowControl ? NetworkClient.unbatcher.flowController.ToString() : "[flow control disabled]")}\n";
        }
    }
}
