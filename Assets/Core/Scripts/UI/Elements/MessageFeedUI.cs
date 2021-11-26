using TMPro;
using UnityEngine;

/// <summary>
/// Shows the message feed as a transient pop-up text on top of the screen
/// </summary>
public class MessageFeedUI : MonoBehaviour
{
    public TextMeshProUGUI messageLog;
    public int maxNumMessageLogMessages = 5;
    public float messageDuration = 8;

    private float lastExpiredMessageTime = -1;

    private bool hasRegisteredCallback = false;

    private void Update()
    {
        MessageFeed logger = MessageFeed.singleton;

        if (logger)
        {
            // this would be in Start but it's networked and we might not know about it until whenever
            if (!hasRegisteredCallback)
            {
                logger.onNewMessage += OnNewMessage;
                hasRegisteredCallback = true;
            }

            float latestExpiredMessageTime = -1;

            for (int i = Mathf.Max(logger.messages.Count - maxNumMessageLogMessages, 0); i < logger.messages.Count; i++)
            {
                if (logger.messages[i].postTime <= Time.time - messageDuration)
                    lastExpiredMessageTime = logger.messages[i].postTime;
            }

            if (lastExpiredMessageTime != latestExpiredMessageTime)
            {
                UpdateMessages();
                lastExpiredMessageTime = latestExpiredMessageTime;
            }
        }
    }

    private void OnNewMessage(string message) => UpdateMessages();

    private void UpdateMessages()
    {
        if (MessageFeed.singleton)
        {
            MessageFeed logger = MessageFeed.singleton;
            string text = "";

            for (int i = Mathf.Max(logger.messages.Count - maxNumMessageLogMessages, 0); i < logger.messages.Count; i++)
            {
                if (logger.messages[i].postTime >= Time.time - messageDuration)
                    text += logger.messages[i].message + "\n";
            }

            if (text != messageLog.text)
                messageLog.text = text;
        }
    }
}
