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

    private List<Image> allLocalTicks = new List<Image>();

    public float timePeriod = 5;

    private float minTime;
    private float maxTime;

    private void Start()
    {
        SetTickListCapacity(allLocalTicks, playerTick, 10);
    }

    // Update is called once per frame
    void LateUpdate()
    {
        Ticker targetTicker = Netplay.singleton.localPlayer != null ? Netplay.singleton.localPlayer.GetComponent<Ticker>() : null;

        if (targetTicker)
        {
            minTime = 0;
            maxTime = Mathf.Max(targetTicker.confirmedPlaybackTime, targetTicker.playbackTime);

            minTime = Mathf.Floor(maxTime / timePeriod) * timePeriod;
            maxTime = Mathf.Ceil(maxTime / timePeriod) * timePeriod;

            earlyTime.text = minTime.ToString();
            lateTime.text = maxTime.ToString();

            SetTickPosition(serverTick.rectTransform, targetTicker.confirmedPlaybackTime);
            SetTickPosition(liveTick.rectTransform, targetTicker.playbackTime);

            for (int i = 0; i < allLocalTicks.Count && i < targetTicker.inputHistory.Count; i++)
            {
                SetTickPosition(allLocalTicks[i].rectTransform, targetTicker.inputHistory.TimeAt(i));
            }
        }
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
