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
    public float minDelay = 0.01f;

    /// <summary>
    /// Call when first receiving a message. remoteTime, if available, should refer to the time that the remote party sent the message, in any format
    /// </summary>
    public void OnMessageReceived(TMessage message, float sentTime)
    {
        receivedMessages.Insert(sentTime, new MessagePack() {
            message = message,
            receivedTime = Time.unscaledTime,
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
                while (nextIndex - 1 >= 0 && receivedMessages.TimeAt(nextIndex - 1) <= Time.unscaledTime - localToRemoteTime)
                    nextIndex--;
            }

            if (receivedMessages.TimeAt(nextIndex) <= Time.unscaledTime - localToRemoteTime)
            {
                message = receivedMessages[nextIndex].message;
                lastPoppedMessageTime = receivedMessages[nextIndex].sentTime;
                return true;
            }
        }

        return false;
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

        receivedMessages.Prune(Mathf.Max(Time.unscaledTime - localToRemoteTime - Mathf.Max(jitterSampleSize, maxDelay * 2f), lastPoppedMessageTime));
    }

    public override string ToString()
    {
        return $"NetFlowController ({typeof(TMessage).Name}):\n-> currentDelay={currentDelay}\nnumMsgs: {receivedMessages.Count}";
    }
}
