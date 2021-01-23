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

    [Header("Win screen")]
    public GameObject winScreen;
    public Text winScreenMessage;

    [Header("Debug")]
    public Text connectStatusText;
    public Text debugText;
    public TMPro.TextMeshProUGUI debugLogText;

    public int numLogLines = 5;
    public bool showHarmlessLogs = true;

    private int numFramesThisSecond = 0;
    private int lastFps = 0;

    private string debugLog;

    bool doRefreshLog = false;

    private void Start()
    {
        Application.logMessageReceived += OnLogMessageReceived;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    void LateUpdate()
    {
        if (!GameManager.singleton || !GameManager.singleton.camera)
            return;

        Player player = GameManager.singleton.camera.currentPlayer;
        bool isMatchFinished = NetGameState.singleton as NetGameStateDeathmatch != null && (NetGameState.singleton as NetGameStateDeathmatch).timeRemaining <= 0;

        numFramesThisSecond++;

        // Player stuff
        ringsText.text = "0";
        scoreText.text = "0";
        timeText.text = "INF";

        if (NetGameState.singleton is NetGameStateDeathmatch netGameStateDeathmatch)
        {
            float timeRemaining = Mathf.Max(netGameStateDeathmatch.timeRemaining, 0f);

            timeText.text = $"{(int)timeRemaining / 60}:{((int)timeRemaining % 60).ToString("D2")}";
        }


        if (player)
        {
            ringsText.text = player.numRings.ToString();
            scoreText.text = player.score.ToString();

            // Weapon stuff
            RingShooting ringShooting = player.GetComponent<RingShooting>();
            for (int i = 0; i < ringShooting.weapons.Count - 1 && i < weaponSlots.Length; i++)
            {
                weaponSlots[i].weapon = ringShooting.weapons[i + 1]; // skip default weapon
                weaponSlots[i].hasWeapon = true;
            }
            for (int j = ringShooting.weapons.Count - 1 /* skip default weapon */; j < weaponSlots.Length; j++)
                weaponSlots[j].hasWeapon = false;

            // Debug stuff
            if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
            {
                lastFps = numFramesThisSecond;
                numFramesThisSecond = 0;
            }

            debugText.text = $"\nPing: {(int)(Netplay.singleton.unreliablePing * 1000f)}ms (reliable: {(int)(Netplay.singleton.reliablePing)})" +
                $"\nFPS: {lastFps}" +
                $"\n{Netplay.singleton.netStat}\nVelocity: {player.movement.velocity} ({player.movement.velocity.magnitude:F2})\nGround: {player.movement.isOnGround}";
        }

        // Scoreboard stuff
        scoreboard.SetActive(Input.GetButton("Scoreboard") || isMatchFinished); // normally we don't use Input, but the HUD is completely client-side so it's fine here
        winScreen.SetActive(isMatchFinished);

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

                scoreboardNames.text += $"{scoreboardPlayer.playerName}\n";
                scoreboardScores.text += $"{scoreboardPlayer.score}\n";
            }
        }

        if (winScreen.activeSelf)
        {
            Player winningPlayer = null;

            foreach (Player candidate in Netplay.singleton.players)
            {
                if (candidate != null)
                {
                    if (winningPlayer == null || candidate.score > winningPlayer.score)
                        winningPlayer = candidate;
                }
            }

            winScreenMessage.text = $"{winningPlayer.playerName} wins!";
        }

        // Connection stuff
        if (Netplay.singleton.connectionStatus != Netplay.ConnectionStatus.Ready)
        {
            connectStatusText.enabled = true;

            switch (Netplay.singleton.connectionStatus)
            {
                case Netplay.ConnectionStatus.Offline: connectStatusText.text = "OFFLINE. RESTART GAME PLZ, SORRY :("; break;
                case Netplay.ConnectionStatus.Disconnected: connectStatusText.text = "DISCONNECTED"; break;
                case Netplay.ConnectionStatus.Connecting: connectStatusText.text = "Connecting..."; break;
            }
        }
        else
        {
            connectStatusText.enabled = false;
        }

        if (doRefreshLog)
        {
            debugLogText.text = debugLog;

            Canvas.ForceUpdateCanvases();

            if (debugLogText.textInfo.lineCount > 0)
            {
                numLogLines = (int)(debugLogText.rectTransform.rect.height / debugLogText.textInfo.lineInfo[0].lineHeight);
                if (debugLogText.textInfo.lineCount > numLogLines)
                {
                    int startCharIdx = debugLogText.textInfo.lineInfo[debugLogText.textInfo.lineCount - numLogLines].firstVisibleCharacterIndex;
                    debugLogText.text = debugLog = debugLog.Substring(startCharIdx);
                }
            }

            doRefreshLog = false;
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log && !showHarmlessLogs)
            return; // don't show everything

        string colorStart = "", colorEnd = "";
        switch (type)
        {
            case LogType.Warning:
                colorStart = "<color=yellow>";
                colorEnd = "</color>";
                break;
            case LogType.Error:
                colorStart = "<color=red>";
                colorEnd = "</color>";
                break;
        }

        debugLog += $"{colorStart}{condition}";

        if (type == LogType.Error || type == LogType.Warning)
        {
            string[] trace = stackTrace.Split('\n');

            // include stack trace where it's important
            debugLog += " @ " + (trace.Length > 0 ? (trace[Mathf.Min(1, trace.Length - 1)]) : "") + colorEnd + "\n";
        }
        else
        {
            debugLog += colorEnd + "\n";
        }

        doRefreshLog = true;
    }

    public void ClearLog()
    {
        debugLogText.text = "";
    }
}
