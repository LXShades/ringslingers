using UnityEngine;

public class NetFlowController<TMessage> where TMessage : struct
{
    private struct MessagePack
    {
        public TMessage message;
        public float receivedTime;
        public float sentTime;
    }
    private HistoryList<MessagePack> receivedMessages = new HistoryList<MessagePack>();

    private float localToRemoteTime = 0f;

    public float currentDelay { get; private set; }
    private float lastPoppedMessageTime = -1f;

    public float jitterSampleSize = 2f;
    public float maxDelay = 0.2f;
    public float minDelay = 0.0f;

    // if a message with a sentTime is pushed below this age in seconds, the flow is reset assuming that the timer must have been reset.
    private float flowResetPeriod = 5f;

    /// <summary>
    /// Call when first receiving a message. remoteTime, if available, should refer to the time that the remote party sent the message, in any format
    /// </summary>
    public void PushMessage(TMessage message, float sentTime)
    {
        if (Mathf.Abs(sentTime - lastPoppedMessageTime) >= flowResetPeriod)
        {
            // this indicates that time has perhaps reset
            Reset();
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

    private void Refresh()
    {
        if (receivedMessages.Count > 1)
        {
            float maxGap = float.MinValue;
            float minGap = float.MaxValue;

            for (int i = 0; i < receivedMessages.Count; i++)
            {
                float gap = receivedMessages[i].receivedTime - receivedMessages[i].sentTime;

                maxGap = Mathf.Max(maxGap, gap);
                minGap = Mathf.Min(minGap, gap);
            }

            currentDelay = Mathf.Clamp(maxGap - minGap, minDelay, maxDelay);
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

        receivedMessages.Prune(Mathf.Max(Time.realtimeSinceStartup - localToRemoteTime - Mathf.Max(jitterSampleSize, maxDelay * 2f)));
    }

    public override string ToString()
    {
        return $"NetFlowController ({typeof(TMessage).Name}):\n-> currentDelay={(int)(currentDelay*1000)}ms\nnumMsgs: {receivedMessages.Count}";
    }
}
