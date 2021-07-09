using UnityEngine;

public class DebugTickTimelineHUD : MonoBehaviour
{
    public float timelineLength = 5f;

    public bool targetLocalPlayer = true;
    public bool showServerTime = false;
    public UnityEngine.UI.Text playerNameText = null;
    public Color32 serverTimeColor = Color.yellow;
    public Color32 playbackTimeColor = Color.green;
    public Color32 confirmedTimeColor = Color.blue;
    public Color32 realtimeColor = new Color32(255, 0, 255, 255);
    public Color32 stateColor = Color.cyan;
    public Color32 inputColor = Color.yellow;

    private TimelineGraphic timeline;

    private void Start()
    {
        timeline = GetComponent<TimelineGraphic>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        Ticker<PlayerInput, CharacterState> targetTicker = null;

        if (targetLocalPlayer && Netplay.singleton.localPlayer != null)
            targetTicker = Netplay.singleton.localPlayer.ticker;
        else
        {
            // first player that isn't local?
            for (int i = 0; i < Netplay.singleton.players.Count; i++)
            {
                if (Netplay.singleton.players[i] && Netplay.singleton.players[i] != Netplay.singleton.localPlayer)
                    targetTicker = Netplay.singleton.players[i].ticker;
            }
        }

        if (targetTicker != null)
        {
            timeline.timeStart = (int)(Mathf.Max(targetTicker.playbackTime, targetTicker.confirmedStateTime) / timelineLength) * timelineLength;
            timeline.timeEnd = timeline.timeStart + timelineLength;

            timeline.ClearDraw();

            timeline.DrawTick(targetTicker.playbackTime, 1.5f, 0.5f, playbackTimeColor, "PT", 0);
            timeline.DrawTick(targetTicker.confirmedStateTime, 1.5f, 0.5f, confirmedTimeColor, "CT", 1);
            timeline.DrawTick(targetTicker.realtimePlaybackTime, 1.5f, 0.5f, realtimeColor, "RT", 2); ;

            for (int i = 0; i < targetTicker.inputHistory.Count; i++)
                timeline.DrawTick(targetTicker.inputHistory.TimeAt(i), 1f, -1f, inputColor);
            for (int i = 0; i < targetTicker.stateHistory.Count; i++)
                timeline.DrawTick(targetTicker.stateHistory.TimeAt(i), 0.5f, 0f, stateColor);

            if (playerNameText)
                playerNameText.text = targetTicker.targetName;

            if (showServerTime)
                timeline.DrawTick(GameTicker.singleton.predictedServerTime, 2f, 2f, serverTimeColor, "ST", 3);
        }
    }
}
