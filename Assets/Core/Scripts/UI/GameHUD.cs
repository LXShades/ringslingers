using System.Collections.Generic;
using System.Text;
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
    public RectTransform[] autoaimBlips = new RectTransform[0];
    public Image crownIcon;
    public Sprite[] crownPerScoreboardPosition = new Sprite[0];

    [Header("Intro")]
    public GameObject levelIntroRoot;
    public TextMeshProUGUI levelNameText;
    public TextMeshProUGUI levelDescriptionText;

    [Header("Status")]
    public Image invincibilityIcon;
    public GameObject gotRedFlagIcon;
    public GameObject gotBlueFlagIcon;
    public GameObject shieldOverlay;
    public GameObject underwaterOverlay;

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

        GameState.GetWhenAvailable<GameState_Map>(this, gsMap =>
        {
            gsMap.onMapChanged += OnMapChanged;

            // Setup level intro
            MapConfiguration config = gsMap.activeMap;
            if (config != null)
            {
                levelNameText.text = config.friendlyName;
                levelDescriptionText.text =
                    $"By: <color=orange>{config.credits}</color>\n" +
                    $"Weapon ammo type: <color=orange>{(config.defaultWeaponAmmoStyle == WeaponAmmoStyle.Quantity ? "Ammo" : "Timed")}</color>\n" +
                    $"Combinable weapons: <color=orange>{(config.defaultWeaponCombinationStyle == WeaponCombinationStyle.Combinable ? "Yes" : "No")}</color>";

                levelIntroRoot.SetActive(true);
            }
        });
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        GamePreferences.onPreferencesChanged -= OnPreferencesChanged;
        if (GameState.Get(out GameState_Map gsMap))
            gsMap.onMapChanged -= OnMapChanged;
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
                {
                    autoaimCrosshair.gameObject.SetActive(true);
                    foreach (RectTransform blip in autoaimBlips)
                        blip.gameObject.SetActive(true);
                }

                Camera camera = GameManager.singleton.camera.unityCamera;
                autoaimCrosshair.position = RectTransformUtility.WorldToScreenPoint(camera, ringShooting.autoAimTarget.transform.position + Vector3.up * 0.5f);

                if (ringShooting.autoaimPredictedBlips.Count > 0)
                {
                    for (int i = 0; i < autoaimBlips.Length; i++)
                    {
                        float transformedBlipIndex = (float)(i + 1) / autoaimBlips.Length * (ringShooting.autoaimPredictedBlips.Count - 1);
                        Vector3 interpolatedPosition = transformedBlipIndex < ringShooting.autoaimPredictedBlips.Count - 1 ?
                            Vector3.Lerp(ringShooting.autoaimPredictedBlips[(int)transformedBlipIndex], ringShooting.autoaimPredictedBlips[(int)transformedBlipIndex + 1], transformedBlipIndex - (int)transformedBlipIndex)
                            : ringShooting.autoaimPredictedBlips[(int)transformedBlipIndex];

                        autoaimBlips[i].position = RectTransformUtility.WorldToScreenPoint(camera, interpolatedPosition);
                    }
                }
            }
            else if (autoaimCrosshair.gameObject.activeSelf)
            {
                autoaimCrosshair.gameObject.SetActive(false);
                foreach (RectTransform blip in autoaimBlips)
                    blip.gameObject.SetActive(false);
            }

            // Update shield overlay
            if ((player.shield != null) != shieldOverlay.activeSelf)
                shieldOverlay.SetActive(player.shield != null);

            // Update statoids (status..es? statopedes?)
            if (player.damageable.invincibilityTimeRemaining > 0f)
                invincibilityIcon.enabled = ((int)(Time.time * 20) & 1) != 0;
            else if (invincibilityIcon.enabled)
                invincibilityIcon.enabled = false;

            // On-screen crown based on your score
            int positionInScoreboard = 1, myScore = player.score;
            for (int i = 0; i < Netplay.singleton.players.Count; i++)
            {
                if (Netplay.singleton.players[i] && Netplay.singleton.players[i].score > myScore)
                    positionInScoreboard++;
            }

            if (positionInScoreboard <= crownPerScoreboardPosition.Length)
            {
                if (crownIcon.sprite != crownPerScoreboardPosition[positionInScoreboard - 1])
                    crownIcon.sprite = crownPerScoreboardPosition[positionInScoreboard - 1];
                if (!crownIcon.enabled)
                    crownIcon.enabled = true;
            }
            else
            {
                if (crownIcon.enabled)
                    crownIcon.enabled = false;
            }
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
            StringBuilder scoreboardNameBuilder = new StringBuilder(512);
            StringBuilder scoreboardScoreBuilder = new StringBuilder(512);
            bool useTeamColours = matchTeams != null;
            int playerPosition = 0;

            System.Array.Sort(orderedPlayers, (a, b) => (a ? a.score : -1) - (b ? b.score : -1) > 0 ? -1 : 1);

            for (int i = 0; i < orderedPlayers.Length; i++)
            {
                Character scoreboardPlayer = orderedPlayers[i];
                if (scoreboardPlayer == null)
                    break;

                // allow for ties between top players
                if (playerPosition < 3 && i > 0 && orderedPlayers[i - 1].score > orderedPlayers[i].score)
                    playerPosition++;

                if (!useTeamColours)
                {
                    scoreboardNameBuilder.Append($"<sprite={playerPosition}>{scoreboardPlayer.playerName}\n");
                    scoreboardScoreBuilder.Append($"{scoreboardPlayer.score}\n");
                }
                else
                {
                    string teamColour = scoreboardPlayer.team.ToFontColor();

                    scoreboardNameBuilder.Append($"<sprite={playerPosition}>{teamColour}{scoreboardPlayer.playerName}</color>\n");
                    scoreboardScoreBuilder.Append($"{teamColour}{scoreboardPlayer.score}</color>\n");
                }
            }

            scoreboardNames.text = scoreboardNameBuilder.ToString();
            scoreboardScores.text = scoreboardScoreBuilder.ToString();
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
            int unreliablePingMs = (int)(Netplay.singleton.unreliablePing * 1000);
            int reliablePingMs = (int)(Netplay.singleton.reliablePing * 1000);
            int smoothPingMs = (int)(GameTicker.singleton != null ? GameTicker.singleton.smoothLocalPlayerPing * 1000 : 0);
            int lastLocalPingMs = ((int)(GameTicker.singleton != null ? GameTicker.singleton.lastLocalPlayerPing * 1000 : 0));

            lastFps = numFramesThisSecond;
            fpsCounter.text = $"FPS {lastFps.ToString()} / Min {(1f / deltaMax).ToString("F1")} / Max {(1f / deltaMin).ToString("F1")}\n" +
                $"Ping/Smooth/Unreliable/Reliable: {lastLocalPingMs.ToString()}/{smoothPingMs.ToString()}/{unreliablePingMs.ToString()}/{reliablePingMs.ToString()}";

            deltaMin = float.MaxValue;
            deltaMax = float.MinValue;
            numFramesThisSecond = 0;
        }

        deltaMin = Mathf.Min(Time.unscaledDeltaTime, deltaMin);
        deltaMax = Mathf.Max(Time.unscaledDeltaTime, deltaMax);

        // Water post process
        bool shouldShowUnderwaterEffect = GameManager.singleton.camera != null && LiquidVolume.GetContainingLiquid(GameManager.singleton.camera.transform.position) != null;
        if (shouldShowUnderwaterEffect != underwaterOverlay.activeSelf)
            underwaterOverlay.SetActive(shouldShowUnderwaterEffect);

        // Debug info
        if (debugDisplay.activeInHierarchy)
            UpdateDebugs();
    }

    private void OnMapChanged(MapConfiguration activeMap)
    {
        // Setup level intro
        if (activeMap != null)
        {
            levelNameText.text = activeMap.friendlyName;
            levelDescriptionText.text =
                $"By: <color=orange>{activeMap.credits}</color>\n" +
                $"Weapon ammo type: <color=orange>{(activeMap.defaultWeaponAmmoStyle == WeaponAmmoStyle.Quantity ? "Ammo" : "Timed")}</color>\n" +
                $"Combinable weapons: <color=orange>{(activeMap.defaultWeaponCombinationStyle == WeaponCombinationStyle.Combinable ? "Yes" : "No")}</color>";

            levelIntroRoot.SetActive(true);
        }
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
