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
    public Text scoreText;
    public Text timeText;
    public Text connectStatusText;

    public Text debugText;
    public Text debugLogText;

    [Header("Debug")]
    public int numLogLines = 8;
    public bool showHarmlessLogs = true;

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

        // Player stuff
        ringsText.text = "0";
        scoreText.text = "0";
        timeText.text = "todo";

        if (player)
        {
            ringsText.text = player.numRings.ToString();

            debugText.text = $"Run Speed: {player.movement.velocity.Horizontal().magnitude.ToString("0.0")}\n"
                + GameManager.singleton.netStat;
        }

        if (GameManager.singleton.connectionStatus == GameManager.NetConnectStatus.Connecting)
        {
            connectStatusText.enabled = true;
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
}
