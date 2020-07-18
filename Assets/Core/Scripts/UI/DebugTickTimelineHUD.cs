using System;
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

    float timeOffset;

    // Update is called once per frame
    void Update()
    {
        if (Netplay.singleton.serverTickHistory.Count == 0)
            return;

        float maxTime = Mathf.Max(Netplay.singleton.serverTickHistory[0].time, World.live.time), minTime = 0;

        minTime = Mathf.Floor(maxTime / timePeriod) * timePeriod;
        maxTime = Mathf.Ceil(maxTime / timePeriod) * timePeriod;

        minTime = Time.time + timeOffset - timePeriod / 2;
        maxTime = Time.time + timeOffset + timePeriod / 2;

        if ((int)(Time.time/2) != (int)((Time.time - Time.deltaTime)/2))
        {
            timeOffset = World.live.time - Time.time;
        }

        earlyTime.text = minTime.ToString();
        lateTime.text = maxTime.ToString();

        //serverTick.rectTransform.anchorMin = serverTick.rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, Netplay.singleton.lastProcessedServerTick.time + Netplay.singleton.lastProcessedServerTick.deltaTime), serverTick.rectTransform.anchorMin.y);
        liveTick.rectTransform.anchorMin = liveTick.rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, World.live.time), liveTick.rectTransform.anchorMin.y);

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

                allServerTicks[i].rectTransform.anchorMin = allServerTicks[i].rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, Netplay.singleton.serverTickHistory[i].time), allServerTicks[i].rectTransform.anchorMin.y);
            }
        }
    }
}
