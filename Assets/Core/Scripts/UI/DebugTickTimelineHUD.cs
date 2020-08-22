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
        if (Netplay.singleton.serverTickHistory.Count == 0)
            return;

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

        SetTickPosition(serverTick.rectTransform, Netplay.singleton.lastReceivedServerTick.playerTicks[Netplay.singleton.localPlayerId].localTime);
        SetTickPosition(liveTick.rectTransform, World.live.localTime);

        /*if (World.live.players[Netplay.singleton.localPlayerId])
        {
            SetTickPosition(serverTick.rectTransform, World.live.players[Netplay.singleton.localPlayerId].serverTime);
        }*/

        int movementHistoryIndexAtServer = 0;

        Player player = World.live.players[Netplay.singleton.localPlayerId];
        History<PlayerInput> movementHistory = null;
        if (player)
        {
            movementHistory = player.movement.inputHistory;

            for (movementHistoryIndexAtServer = 0; movementHistoryIndexAtServer < movementHistory.Count; movementHistoryIndexAtServer++)
            {
                if (Netplay.singleton.lastReceivedServerTick.playerTicks[Netplay.singleton.localPlayerId].localTime == movementHistory.TimeAt(movementHistoryIndexAtServer))
                    break;
            }

            if (movementHistoryIndexAtServer == movementHistory.Count)
                movementHistoryIndexAtServer = 0;

            for (int i = 0; i < allLocalTicks.Count; i++)
            {
                if (i >= movementHistoryIndexAtServer)
                    allLocalTicks[i].enabled = false;
                else
                {
                    allLocalTicks[i].enabled = true;
                    SetTickPosition(allLocalTicks[i].rectTransform, movementHistory.TimeAt(i));
                }
            }
        }

        SetTickListCapacity(allServerTicks, pastServerTick, Netplay.singleton.serverTickHistory.Count);

        if (movementHistoryIndexAtServer != -1)
            SetTickListCapacity(allLocalTicks, pastLocalTick, movementHistoryIndexAtServer);

        // show past tick times
        for (int i = 0; i < allServerTicks.Count; i++)
        {
            if (i >= Netplay.singleton.serverTickHistory.Count)
            {
                allServerTicks[i].enabled = false;
            }
            else
            {
                allServerTicks[i].enabled = true;
                SetTickPosition(allServerTicks[i].rectTransform, Netplay.singleton.serverTickHistory[i].playerTicks[Netplay.singleton.localPlayerId].localTime);
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
