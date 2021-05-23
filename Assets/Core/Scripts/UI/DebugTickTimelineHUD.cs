using UnityEngine;

public class DebugTickTimelineHUD : MonoBehaviour
{
    public float timelineLength = 5f;

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
        Ticker targetTicker = Netplay.singleton.localPlayer != null ? Netplay.singleton.localPlayer.GetComponent<Ticker>() : null;

        if (targetTicker)
        {
            timeline.timeStart = (int)(Mathf.Max(targetTicker.playbackTime, targetTicker.confirmedPlaybackTime) / timelineLength) * timelineLength;
            timeline.timeEnd = timeline.timeStart + timelineLength;

            timeline.ClearDraw();

            timeline.DrawTick(targetTicker.playbackTime, 2f, 0f, playbackTimeColor, "PT", 0);
            timeline.DrawTick(targetTicker.confirmedPlaybackTime, 2f, 0f, confirmedTimeColor, "CT", 1);
            timeline.DrawTick(targetTicker.realtimePlaybackTime, 2f, 0f, realtimeColor, "RT", 2); ;

            for (int i = 0; i < targetTicker.inputHistory.Count; i++)
            {
                timeline.DrawTick(targetTicker.inputHistory.TimeAt(i), 1f, 0f, inputColor);
            }
            for (int i = 0; i < targetTicker.stateHistory.Count; i++)
            {
                timeline.DrawTick(targetTicker.stateHistory.TimeAt(i), 0.5f, 1f, stateColor);
            }
        }
    }
}
