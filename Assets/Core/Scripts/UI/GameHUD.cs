using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game HUD. Since this is purely visual, we can use a regular MonoBehaviour
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Gameplay")]
    public TextMeshProUGUI ringsText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public RectTransform autoaimCrosshair;
    public GameObject shieldOverlay;

    [Header("Intro")]
    public GameObject levelIntroRoot;
    public TextMeshProUGUI levelNameText;
    public TextMeshProUGUI levelDescriptionText;

    [Header("Status")]
    public Image invincibilityIcon;
    public GameObject gotRedFlagIcon;
    public GameObject gotBlueFlagIcon;

    [Header("Weapons")]
    public GameObject weaponWheel;
    public WeaponSlotUI weaponSlotPrefab;
    public Transform equippedWeaponArea;
    public Transform unequippedWeaponArea;
    public RingWeaponSettingsAsset[] weaponTypes;

    [Header("Teams")]
    public GameObject teamsHud;
    public TextMeshProUGUI redTeamPoints;
    public TextMeshProUGUI blueTeamPoints;
    public GameObject redFlagStolen;
    public GameObject blueFlagStolen;

    [Header("Scoreboard")]
    public GameObject scoreboard;
    public TextMeshProUGUI scoreboardNames;
    public TextMeshProUGUI scoreboardScores;

    [Header("Win screen")]
    public GameObject winScreen;
    public Text winScreenMessage;
    public Text winScreenCountdown;

    [Header("Debug")]
    public Text fpsCounter;
    public GameObject debugDisplay;
    public Text connectStatusText;
    public Text debugText;
    public TextMeshProUGUI debugLogText;

    public int numLogLines = 5;
    public bool showHarmlessLogs = true;

    private int numFramesThisSecond = 0;
    private int lastFps = 0;

    private float deltaMin = float.MaxValue;
    private float deltaMax = float.MinValue;

    private string debugLog;

    private List<WeaponSlotUI> weaponSlots = new List<WeaponSlotUI>();

    bool doRefreshLog = false;

    private void Start()
    {
        Application.logMessageReceived += OnLogMessageReceived;

        debugDisplay.SetActive(GamePreferences.isDebugInfoEnabled);
        GamePreferences.onPreferencesChanged += OnPreferencesChanged;

        // Spawn weapon slots
        for (int i = 0; i < weaponTypes.Length; i++)
        {
            WeaponSlotUI slot = Instantiate(weaponSlotPrefab, equippedWeaponArea);
            slot.weapon = new RingWeapon()
            {
                ammo = 0,
                weaponType = weaponTypes[i]
            };
            slot.hasWeapon = false;
            weaponSlots.Add(slot);
        }

        // Setup level intro
        LevelConfigurationComponent config = FindObjectOfType<LevelConfigurationComponent>();
        if (config != null)
        {
            levelNameText.text = config.configuration.friendlyName;
            levelDescriptionText.text = 
                $"By: <color=orange>{config.configuration.credits}</color>\n" +
                $"Weapon Limit: <color=orange>{(config.configuration.defaultWeaponAmmoStyle == WeaponAmmoStyle.Quantity ? "Ammo" : "Timer")}</color>\n" +
                $"Combinable weapons: <color=orange>{(config.configuration.defaultWeaponCombinationStyle == WeaponCombinationStyle.Combinable ? "Yes" : "No")}</color>";

            levelIntroRoot.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        GamePreferences.onPreferencesChanged -= OnPreferencesChanged;
    }

    void LateUpdate()
    {
        if (!GameManager.singleton || !GameManager.singleton.camera)
            return;

        Character player = GameManager.singleton.camera.currentPlayer;
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

        // Team stuff
        if (MatchState.Get(out MatchTeams matchTeams))
        {
            if (!teamsHud.gameObject.activeSelf)
                teamsHud.SetActive(true);

            redTeamPoints.text = matchTeams.redTeamPoints.ToString();
            blueTeamPoints.text = matchTeams.blueTeamPoints.ToString();
        }
        else
        {
            if (redTeamPoints.gameObject.activeSelf)
                teamsHud.SetActive(false);
        }

        // CTF stuff
        if (MatchState.Get(out MatchFlags matchFlags))
        {
            bool isRedFlagStolen = matchFlags.redFlag != null && (matchFlags.redFlag.currentCarrier != -1);
            bool isBlueFlagStolen = matchFlags.blueFlag != null && (matchFlags.blueFlag.currentCarrier != -1);
            bool hasGotRedFlag = matchFlags.redFlag != null && (matchFlags.redFlag.currentCarrier == Netplay.singleton.localPlayerId);
            bool hasGotBlueFlag = matchFlags.blueFlag != null && (matchFlags.blueFlag.currentCarrier == Netplay.singleton.localPlayerId);

            if (redFlagStolen.activeSelf != isRedFlagStolen)
                redFlagStolen.SetActive(isRedFlagStolen);
            if (blueFlagStolen.activeSelf != isBlueFlagStolen)
                blueFlagStolen.SetActive(isBlueFlagStolen);

            if (gotRedFlagIcon.activeSelf != hasGotRedFlag)
                gotRedFlagIcon.SetActive(hasGotRedFlag);
            if (gotBlueFlagIcon.activeSelf != hasGotBlueFlag)
                gotBlueFlagIcon.SetActive(hasGotBlueFlag);
        }
        else
        {
            if (redFlagStolen.activeSelf)
            {
                redFlagStolen.SetActive(false);
                blueFlagStolen.SetActive(false);
            }

            if (gotRedFlagIcon.activeSelf)
            {
                gotRedFlagIcon.SetActive(false);
                gotBlueFlagIcon.SetActive(false);
            }
        }

        // Player stuff
        if (player)
        {
            ringsText.text = player.numRings.ToString();
            scoreText.text = player.score.ToString();

            // Update weapon wheel
            bool shouldDisplayWeaponWheel = GameManager.singleton.input.Gameplay.WeaponWheel.ReadValue<float>() > 0.5f; // this is so dumb I fricken swear

            if (shouldDisplayWeaponWheel != weaponWheel.activeSelf)
            {
                weaponWheel.SetActive(shouldDisplayWeaponWheel);
                equippedWeaponArea.gameObject.SetActive(!shouldDisplayWeaponWheel);
                unequippedWeaponArea.gameObject.SetActive(!shouldDisplayWeaponWheel);
            }

            CharacterShooting ringShooting = player.GetComponent<CharacterShooting>();
            if (!shouldDisplayWeaponWheel)
            {
                // Update weapon panels
                for (int i = 0; i < weaponSlots.Count; i++)
                {
                    bool hasWeapon = false;
                    for (int j = 0; j < ringShooting.weapons.Count; j++)
                    {
                        if (ringShooting.weapons[j].weaponType == weaponSlots[i].weapon.weaponType)
                        {
                            hasWeapon = true;
                            weaponSlots[i].weapon = ringShooting.weapons[j];
                            weaponSlots[i].hasWeapon = true;
                            break;
                        }
                    }

                    if (!hasWeapon)
                        weaponSlots[i].hasWeapon = false;

                    bool isEquipped = ringShooting.equippedWeapons.Count == 0 || ringShooting.equippedWeapons.IndexOf(weaponSlots[i].weapon.weaponType) != -1;

                    if (isEquipped != (weaponSlots[i].transform.parent == equippedWeaponArea))
                    {
                        Transform newParent = isEquipped ? equippedWeaponArea : unequippedWeaponArea;
                        int siblingIndex = 0;
                        for (int j = 0; j < i; j++)
                        {
                            if (weaponSlots[j].transform.parent == newParent)
                                ++siblingIndex;
                        }

                        weaponSlots[i].transform.SetParent(newParent, false);
                        weaponSlots[i].transform.SetSiblingIndex(siblingIndex + 1);
                    }
                }
            }

            // Update autoaim crosshair
            if (ringShooting.autoAimTarget)
            {
                if (!autoaimCrosshair.gameObject.activeSelf)
                    autoaimCrosshair.gameObject.SetActive(true);

                autoaimCrosshair.position = RectTransformUtility.WorldToScreenPoint(Camera.main, ringShooting.autoAimTarget.transform.position + Vector3.up * 0.5f);
            }
            else if (autoaimCrosshair.gameObject.activeSelf)
                autoaimCrosshair.gameObject.SetActive(false);

            // Update shield overlay
            if ((player.shield != null) != shieldOverlay.activeSelf)
                shieldOverlay.SetActive(player.shield != null);

            // Update statoids (status..es? statusopedes?)
            if (player.damageable.invincibilityTimeRemaining > 0f)
                invincibilityIcon.enabled = ((int)(Time.time * 20) & 1) != 0;
            else if (invincibilityIcon.enabled)
                invincibilityIcon.enabled = false;
        }
        else
        {
            ringsText.text = "0";
            scoreText.text = "0";
        }

        // Scoreboard stuff
        scoreboard.SetActive(GameManager.singleton.input.Gameplay.ViewScores.ReadValue<float>() > 0.5f || isMatchFinished); // normally we don't use Input, but the HUD is completely client-side so it's fine here

        if (scoreboard.activeSelf)
        {
            // Refresh scoreboard info
            Character[] orderedPlayers = Netplay.singleton.players.ToArray();
            bool useTeamColours = matchTeams != null;

            System.Array.Sort(orderedPlayers, (a, b) => (a ? a.score : -1) - (b ? b.score : -1) > 0 ? -1 : 1);

            scoreboardNames.text = "";
            scoreboardScores.text = "";

            foreach (Character scoreboardPlayer in orderedPlayers)
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

        // FPS
        if ((int)Time.unscaledTime != (int)(Time.unscaledTime - Time.unscaledDeltaTime))
        {
            lastFps = numFramesThisSecond;
            fpsCounter.text = $"FPS {lastFps.ToString()} / Min {(1f / deltaMax).ToString("F1")} / Max {(1f / deltaMin).ToString("F1")}\n" +
                $"Ping/Unreliable/Reliable: " +
                $"{((int)(GameTicker.singleton != null ? GameTicker.singleton.localPlayerPing * 1000 : 0)).ToString()}/{((int)(Netplay.singleton.unreliablePing * 1000)).ToString()}/{((int)(Netplay.singleton.reliablePing * 1000)).ToString()}";

            deltaMin = float.MaxValue;
            deltaMax = float.MinValue;
            numFramesThisSecond = 0;
        }

        deltaMin = Mathf.Min(Time.unscaledDeltaTime, deltaMin);
        deltaMax = Mathf.Max(Time.unscaledDeltaTime, deltaMax);

        // Debug info
        if (debugDisplay.activeInHierarchy)
            UpdateDebugs();
    }

    private void UpdateDebugs()
    {
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

    private void OnPreferencesChanged()
    {
        debugDisplay.SetActive(GamePreferences.isDebugInfoEnabled);
    }

    public void ClearLog()
    {
        debugLogText.text = "";
    }
}
