using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game HUD. Since this is purely visual, we can use a regular MonoBehaviour
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Main")]
    public Text ringsText;
    public Text scoreText;
    public Text timeText;
    public WeaponSlotUI[] weaponSlots = new WeaponSlotUI[0];

    [Header("Scoreboard")]
    public GameObject scoreboard;
    public Text scoreboardNames;
    public Text scoreboardScores;

    [Header("Debug")]
    public Text connectStatusText;
    public Text debugText;
    public Text debugLogText;

    public int numLogLines = 6;
    public bool showHarmlessLogs = true;

    private int numFramesThisSecond = 0;
    private int lastFps = 0;
    private float lastPing = 0;

    private void Start()
    {
        Application.logMessageReceived += OnLogMessageReceived;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    void Update()
    {
        Player player = GameManager.singleton.camera.currentPlayer;

        numFramesThisSecond++;

        // Player stuff
        ringsText.text = "0";
        scoreText.text = "0";
        timeText.text = "todo";

        if (player)
        {
            ringsText.text = player.numRings.ToString();
            scoreText.text = player.score.ToString();

            // Weapon stuff
            RingShooting ringShooting = player.GetComponent<RingShooting>();
            for (int i = 0; i < ringShooting.equippedWeapons.Count && i < weaponSlots.Length; i++)
                weaponSlots[i].weapon = ringShooting.equippedWeapons[i];
            for (int j = ringShooting.equippedWeapons.Count; j < weaponSlots.Length; j++)
                weaponSlots[j].weapon = null;

            // Debug stuff
            if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
            {
                lastFps = numFramesThisSecond;
                lastPing = Netplay.singleton.GetPing();
                numFramesThisSecond = 0;
            }

            debugText.text = $"Local frame: {World.live.time.ToString("#.00")}" +
                $"\nServer frame: {Netplay.singleton.serverTickHistory[0].tick.time.ToString("#.00")}" +
                $"\nPing: {(int)(lastPing * 1000)}ms" +
                $"\nFPS: {lastFps}" +
                $"\nRun Speed: {player.movement.velocity.Horizontal().magnitude.ToString("0.0")}" +
                $"\n{Netplay.singleton.netStat}";
        }

        // Scoreboard stuff
        scoreboard.SetActive(Input.GetButton("Scoreboard")); // normally we don't use Input, but the HUD is completely client-side so it's fine here

        if (scoreboard.activeSelf)
        {
            // Refresh scoreboard info
            Player[] orderedPlayers = (Player[])Netplay.singleton.players.Clone();

            System.Array.Sort(orderedPlayers, (a, b) => (a ? a.score : 0) - (b ? b.score : 0) > 0 ? -1 : 1);

            scoreboardNames.text = "";
            scoreboardScores.text = "";

            foreach (Player scoreboardPlayer in orderedPlayers)
            {
                if (scoreboardPlayer == null)
                    break;

                scoreboardNames.text += $"{scoreboardPlayer.name}\n";
                scoreboardScores.text += $"{scoreboardPlayer.score}\n";
            }
        }

        // Connection stuff
        if (Netplay.singleton.connectionStatus != Netplay.ConnectionStatus.Ready)
        {
            connectStatusText.enabled = true;

            switch (Netplay.singleton.connectionStatus)
            {
                case Netplay.ConnectionStatus.Disconnected:
                    connectStatusText.text = "DISCONNECTED";
                    break;
                case Netplay.ConnectionStatus.Connecting:
                    connectStatusText.text = "Connecting...";
                    break;
            }
        }
        else
        {
            connectStatusText.enabled = false;
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log && !showHarmlessLogs)
            return; // don't show everything

        List<string> debugLog = new List<string>(debugLogText.text.Split('\n'));
        string[] trace = stackTrace.Split('\n');

        debugLog.Add(condition + ": " + (trace.Length > 0 ? (trace[Mathf.Min(1, trace.Length - 1)]) : ""));

        if (debugLog.Count > numLogLines)
        {
            debugLog.RemoveAt(0);
        }

        debugLogText.text = string.Join("\n", debugLog);
    }

    public void ClearLog()
    {
        debugLogText.text = "";
    }
}
