using UnityEngine;
using UnityEngine.UI;

public class ServerTimeMonitor : MonoBehaviour
{
    public RectTransform balanceLine;
    public float range = 0.2f;

    public GraphGraphic timeGraphs;

    public Text timeLabel;
    public Text leftLabel;
    public Text rightLabel;

    private double lastServerTime;
    private double lastLocalTime;

    private GraphGraphic.GraphCurve predictedServerTimeCurve;    // time of self, based on predicted server time
    private GraphGraphic.GraphCurve lastReceivedServerTimeCurve; // time last received from server
    private GraphGraphic.GraphCurve serverLocalTimeCurve;        // time of self on server, as last recieved from server

    private double lastAddedServerTickRealtime;

    private void Awake()
    {
        leftLabel.text = $"{Mathf.RoundToInt(-range * 100)}%";
        rightLabel.text = $"{Mathf.RoundToInt(range * 100)}%";

        lastReceivedServerTimeCurve = timeGraphs.AddCurve(Color.red);
        predictedServerTimeCurve = timeGraphs.AddCurve(Color.yellow);
        serverLocalTimeCurve = timeGraphs.AddCurve(Color.blue);
    }

    private void LateUpdate()
    {
        if (GameTicker.singleton != null)
        {
            float parentWidth = (balanceLine.transform.parent as RectTransform).sizeDelta.x; // .rect.width maybe? sizeDelta seems to do whatever it wants
            float gameSpeed = (float)((GameTicker.singleton.predictedServerTime - lastServerTime) / (Time.time - lastLocalTime));

            balanceLine.anchoredPosition = new Vector2((gameSpeed - 1f) * parentWidth / 2f / range, 0f);

            timeLabel.text = $"{((gameSpeed - 1f) * 100).ToString("F1")}%";

            // we only need to add points as server ticks come in really (especially for the server data)
            if (GameTicker.singleton.realtimeOfLastProcessedServerTick > lastAddedServerTickRealtime)
            {
                // Our local predicted time
                predictedServerTimeCurve.data.Insert(Time.realtimeSinceStartup, (float)(GameTicker.singleton.predictedServerTime - Time.realtimeSinceStartup));

                // Times on server: server time, and our local time from the server's perspective
                double serverTimeOnGraph = Time.realtimeSinceStartup - (GameTicker.singleton.predictedServerTime - GameTicker.singleton.lastProcessedServerTick.serverTime);
                lastReceivedServerTimeCurve.data.Insert(serverTimeOnGraph, (float)(GameTicker.singleton.lastProcessedServerTick.serverTime - Time.realtimeSinceStartupAsDouble));
                serverLocalTimeCurve.data.Insert(serverTimeOnGraph, (float)(GameTicker.singleton.lastProcessedServerTick.serverTime + GameTicker.singleton.lastProcessedServerTick.lastClientEarlyness - Time.realtimeSinceStartupAsDouble));

                timeGraphs.ClearTimeAfter(Time.realtimeSinceStartup + 2f);

                lastAddedServerTickRealtime = GameTicker.singleton.realtimeOfLastProcessedServerTick;

                timeGraphs.Redraw();
            }


            lastServerTime = GameTicker.singleton.predictedServerTime;
            lastLocalTime = Time.timeAsDouble;
        }
    }
}
