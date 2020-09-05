using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugTickTimelineHUD : MonoBehaviour
{
    public Text earlyTime;
    public Text lateTime;

    public Image liveTick;
    public Image serverTick;
    public Image playerTick;
    public Image pastServerTick;
    public Image pastLocalTick;

    private List<Image> allServerTicks = new List<Image>();
    private List<Image> allLocalTicks = new List<Image>();

    public float timePeriod = 5;

    private float minTime;
    private float maxTime;

    private float timeOffset;

    // Update is called once per frame
    void Update()
    {
        minTime = 0;
        maxTime = World.live.localTime;

        minTime = Mathf.Floor(maxTime / timePeriod) * timePeriod;
        maxTime = Mathf.Ceil(maxTime / timePeriod) * timePeriod;

        //minTime = Time.time + timeOffset - timePeriod / 2;
        //maxTime = Time.time + timeOffset + timePeriod / 2;

        if ((int)(Time.time/2) != (int)((Time.time - Time.deltaTime)/2))
        {
            timeOffset = World.live.gameTime - Time.time;
        }

        earlyTime.text = minTime.ToString();
        lateTime.text = maxTime.ToString();

        SetTickPosition(liveTick.rectTransform, World.live.localTime);
    }

    void SetTickListCapacity(List<Image> list, Image prefab, int number)
    {
        if (list.Count < number)
        {
            for (int i = list.Count; i < number; i++)
            {
                list.Add(Instantiate(prefab, prefab.rectTransform.parent).GetComponent<Image>());
            }
        }
    }

    void SetTickPosition(RectTransform tick, float time)
    {
        tick.anchorMin = tick.anchorMax = new Vector2(Mathf.InverseLerp(minTime, maxTime, time), serverTick.rectTransform.anchorMin.y);
    }
}
