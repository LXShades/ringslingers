using System.Collections.Generic;
using UnityEngine;

public class NetFlowController<TMessage> where TMessage : struct
{
    private struct MessagePack
    {
        public TMessage message;
        public float receivedTime;
        public float sentTime;
    }
    private TimelineList<MessagePack> receivedMessages = new TimelineList<MessagePack>();

    private float localToRemoteTime = 0f;

    public float currentDelay { get; private set; }
    private float lastPoppedMessageTime = -1f;

    // if a message with a sentTime is pushed below this age in seconds, the flow is reset on an assumption that the timer must have been reset.
    private float flowResetPeriod = 5f;

    public FlowControlSettings flowControlSettings = FlowControlSettings.Default;

    /// <summary>
    /// Call when first receiving a message. remoteTime, if available, should refer to the time that the remote party sent the message, in any format
    /// </summary>
    public void PushMessage(TMessage message, float sentTime)
    {
        if (Mathf.Abs(sentTime - lastPoppedMessageTime) >= flowResetPeriod)
        {
            // this indicates that time has perhaps reset
            Reset();
            lastPoppedMessageTime = sentTime - 0.01f;
            Log.WriteWarning($"Resetting net flow due to pushing a message older than the reset period");
        }

        receivedMessages.Insert(sentTime, new MessagePack() {
            message = message,
            receivedTime = Time.realtimeSinceStartup,
            sentTime = sentTime
        });

        Refresh();
    }

    public bool TryPopMessage(out TMessage message, bool skipOutdatedMessages)
    {
        int nextIndex = receivedMessages.ClosestIndexAfter(lastPoppedMessageTime, 0f);

        message = default;
        if (nextIndex != -1)
        {
            if (skipOutdatedMessages)
            {
                while (nextIndex - 1 >= 0 && receivedMessages.TimeAt(nextIndex - 1) <= Time.realtimeSinceStartup - localToRemoteTime)
                    nextIndex--;
            }

            if (receivedMessages.TimeAt(nextIndex) <= Time.realtimeSinceStartup - localToRemoteTime)
            {
                message = receivedMessages[nextIndex].message;
                lastPoppedMessageTime = receivedMessages[nextIndex].sentTime;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resets the flow controller meaning time can start again from 0
    /// </summary>
    public void Reset()
    {
        receivedMessages.Clear();
        lastPoppedMessageTime = 0f;
    }

    private List<float> sortedGaps = new List<float>(100);

    private void Refresh()
    {
        if (receivedMessages.Count > 1)
        {
            sortedGaps.Clear();

            for (int i = 0; i < receivedMessages.Count; i++)
                sortedGaps.Add(receivedMessages[i].receivedTime - receivedMessages[i].sentTime);

            sortedGaps.Sort();

            float minGap = sortedGaps[0];
            int topPercentileIndex = (int)Mathf.Min((1f - flowControlSettings.upperPercentile / 100f) * sortedGaps.Count);
            currentDelay = Mathf.Clamp(
                Mathf.Min(sortedGaps[topPercentileIndex], sortedGaps.Count - 1) + flowControlSettings.addToDelay - minGap,
                flowControlSettings.minDelay,
                flowControlSettings.maxDelay);
            localToRemoteTime = minGap + currentDelay;
        }
        else
        {
            currentDelay = 0f;

            if (receivedMessages.Count == 1)
                localToRemoteTime = receivedMessages[0].receivedTime - receivedMessages.TimeAt(0);
            else
                localToRemoteTime = 0f; // who knows!
        }

        receivedMessages.TrimBefore(Mathf.Max(Time.realtimeSinceStartup - localToRemoteTime - Mathf.Max(flowControlSettings.jitterSampleSize, flowControlSettings.maxDelay * 2f)));
    }

    public override string ToString()
    {
        return $"NetFlowController ({typeof(TMessage).Name}):\n-> currentDelay={(int)(currentDelay*1000)}ms\nnumMsgs: {receivedMessages.Count}";
    }
}

[System.Serializable]
public struct FlowControlSettings
{
    public static FlowControlSettings Default = new FlowControlSettings()
    {
        jitterSampleSize = 3f,
        upperPercentile = 1f,
        maxDelay = 0.1f,
        minDelay = 0f
    };

    public float jitterSampleSize;

    public float upperPercentile; // as a percentage. 1 means highest 1% of jitter controls the overall delay of the flow
    public float addToDelay;

    public float maxDelay;
    public float minDelay;
}