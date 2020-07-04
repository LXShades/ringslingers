using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugTickTimelineHUD : MonoBehaviour
{
    public Text earlyTime;
    public Text lateTime;

    public Image serverTick;
    public Image liveTick;
    public Image pastServerTick;

    private List<Image> allServerTicks = new List<Image>();

    public float timePeriod = 5;

    // Update is called once per frame
    void Update()
    {
        float maxTime = Mathf.Max(Netplay.singleton.serverTickHistory[0].tick.time, GameState.live.time), minTime = 0;

        minTime = Mathf.Floor(maxTime / timePeriod) * timePeriod;
        maxTime = Mathf.Ceil(maxTime / timePeriod) * timePeriod;

        earlyTime.text = minTime.ToString();
        lateTime.text = maxTime.ToString();

        serverTick.rectTransform.anchorMin = serverTick.rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, Netplay.singleton.lastProcessedServerTick.time + Netplay.singleton.lastProcessedServerTick.deltaTime), serverTick.rectTransform.anchorMin.y);
        liveTick.rectTransform.anchorMin = liveTick.rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, GameState.live.time), liveTick.rectTransform.anchorMin.y);

        if (allServerTicks.Count < Netplay.singleton.serverTickHistory.Count)
        {
            for (int i = allServerTicks.Count; i < Netplay.singleton.serverTickHistory.Count; i++)
            {
                allServerTicks.Add(Instantiate(pastServerTick, pastServerTick.rectTransform.parent).GetComponent<Image>());
            }
        }

        for (int i = 0; i < allServerTicks.Count; i++)
        {
            if (i >= Netplay.singleton.serverTickHistory.Count)
            {
                allServerTicks[i].enabled = false;
            }
            else
            {
                allServerTicks[i].enabled = true;

                allServerTicks[i].rectTransform.anchorMin = allServerTicks[i].rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, Netplay.singleton.serverTickHistory[i].tick.time), allServerTicks[i].rectTransform.anchorMin.y);
            }
        }
    }
}
