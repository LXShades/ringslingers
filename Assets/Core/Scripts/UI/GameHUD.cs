using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game HUD. Since this is purely visual, we can use a regular MonoBehaviour
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Gameplay")]
    public Text ringsText;
    public Text scoreText;
    public Text timeText;
    public RectTransform autoaimCrosshair;
    public WeaponSlotUI[] weaponSlots = new WeaponSlotUI[0];

    [Header("Teams")]
    public Text redTeamPoints;
    public Text blueTeamPoints;

    [Header("Scoreboard")]
    public GameObject scoreboard;
    public TextMeshProUGUI scoreboardNames;
    public TextMeshProUGUI scoreboardScores;

    [Header("Win screen")]
    public GameObject winScreen;
    public Text winScreenMessage;
    public Text winScreenCountdown;

    [Header("Debug")]
    public Text connectStatusText;
    public Text debugText;
    public TextMeshProUGUI debugLogText;

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
        bool isMatchFinished = MatchState.singleton != null ? MatchState.singleton.IsWinScreen : false;

        numFramesThisSecond++;

        // Match stuff
        if (MatchState.Get(out MatchTimer matchTimer))
        {
            float time = Mathf.Max(matchTimer.timeRemaining, 0f);

            timeText.text = $"{(int)time / 60}:{((int)time % 60).ToString("D2")}";
        }
        else
        {
            timeText.text = "--:--";
        }

        // CTF stuff
        if (MatchState.Get(out MatchTeams matchTeams))
        {
            if (!redTeamPoints.gameObject.activeSelf)
            {
                redTeamPoints.gameObject.SetActive(true);
                blueTeamPoints.gameObject.SetActive(true);
            }

            redTeamPoints.text = $"RED TEAM\n{matchTeams.redTeamPoints}";
            blueTeamPoints.text = $"BLUE TEAM\n{matchTeams.blueTeamPoints}";
        }
        else
        {
            if (redTeamPoints.gameObject.activeInHierarchy)
            {
                redTeamPoints.gameObject.SetActive(false);
                blueTeamPoints.gameObject.SetActive(false);
            }
        }

        // Player stuff
        ringsText.text = "0";
        scoreText.text = "0";

        if (player)
        {
            ringsText.text = player.numRings.ToString();
            scoreText.text = player.score.ToString();

            // Update weapon panels
            RingShooting ringShooting = player.GetComponent<RingShooting>();
            for (int i = 0; i < ringShooting.weapons.Count - 1 && i < weaponSlots.Length; i++)
            {
                weaponSlots[i].weapon = ringShooting.weapons[i + 1]; // skip default weapon
                weaponSlots[i].hasWeapon = true;
            }

            for (int j = Mathf.Max(ringShooting.weapons.Count - 1 /* skip default weapon */, 0); j < weaponSlots.Length; j++)
                weaponSlots[j].hasWeapon = false;

            // Update autoaim crosshair
            if (ringShooting.autoAimTarget)
            {
                if (!autoaimCrosshair.gameObject.activeSelf)
                    autoaimCrosshair.gameObject.SetActive(true);

                autoaimCrosshair.position = RectTransformUtility.WorldToScreenPoint(Camera.main, ringShooting.autoAimTarget.transform.position + Vector3.up * 0.5f);
            }
            else if (autoaimCrosshair.gameObject.activeSelf)
                autoaimCrosshair.gameObject.SetActive(false);

            // Debug stuff
            if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
            {
                lastFps = numFramesThisSecond;
                numFramesThisSecond = 0;
            }

            debugText.text = $"\nPing: {(int)(Netplay.singleton.unreliablePing * 1000f)}ms (reliable: {(int)(Netplay.singleton.reliablePing)})" +
                $"\nFPS: {lastFps}" +
                $"\n{Netplay.singleton.netStat}\nVelocity: {player.movement.velocity} ({player.movement.velocity.magnitude:F2})\nGround: {player.movement.isOnGround}\nGroundNml: {player.movement.groundNormal}\n" +
                $"GroundVel: {player.movement.groundVelocity}\nUp: {player.movement.up}\nRunVel: {player.movement.runVelocity}\n";

            // debug stuff for other players in the same scene
            if (PlayerTicker.singleton)
                debugText.text += $"Ticker info: ===\n{PlayerTicker.singleton.DebugInfo()}";
        }

        // Scoreboard stuff
        scoreboard.SetActive(GameManager.singleton.input.Gameplay.ViewScores.ReadValue<float>() > 0.5f || isMatchFinished); // normally we don't use Input, but the HUD is completely client-side so it's fine here

        if (scoreboard.activeSelf)
        {
            // Refresh scoreboard info
            Player[] orderedPlayers = Netplay.singleton.players.ToArray();
            bool useTeamColours = matchTeams != null;

            System.Array.Sort(orderedPlayers, (a, b) => (a ? a.score : -1) - (b ? b.score : -1) > 0 ? -1 : 1);

            scoreboardNames.text = "";
            scoreboardScores.text = "";

            foreach (Player scoreboardPlayer in orderedPlayers)
            {
                if (scoreboardPlayer == null)
                    break;

                if (!useTeamColours)
                {
                    scoreboardNames.text += $"{scoreboardPlayer.playerName}\n";
                    scoreboardScores.text += $"{scoreboardPlayer.score}\n";
                }
                else
                {
                    string teamColour = scoreboardPlayer.team.ToFontColor();

                    scoreboardNames.text += $"{teamColour}{scoreboardPlayer.playerName}</color>\n";
                    scoreboardScores.text += $"{teamColour}{scoreboardPlayer.score}</color>\n";
                }
            }
        }

        // Win screen stuff
        winScreen.SetActive(isMatchFinished);

        if (isMatchFinished)
        {
            winScreenMessage.text = $"{MatchState.singleton.GetWinners()} wins!";
            winScreenCountdown.text = $"Next round in {((int)MatchState.singleton.timeTilRestart).ToString()}...";
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

        // Debug logging stuff
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
