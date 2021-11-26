using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the message history as a long scrollable text
/// </summary>
public class MessageHistoryUI : MonoBehaviour
{
    public TextMeshProUGUI messageLog;
    public ScrollRect scrollRect;

    private bool hasRegisteredCallback = false;

    private bool isScrollLockedAtBottom = true;

    private System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(8192);

    private void OnEnable()
    {
        if (scrollRect)
            scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    private void OnDisable()
    {
        if (scrollRect)
            scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
    }

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
        }

        if (isScrollLockedAtBottom && scrollRect != null && scrollRect.verticalNormalizedPosition >= 0.01f)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    private void OnScrollChanged(Vector2 position)
    {
        isScrollLockedAtBottom = position.y <= 0.0f;
    }

    private void OnNewMessage(string message)
    {
        if (MessageFeed.singleton)
        {
            stringBuilder.Clear();
            for (int i = 0; i < MessageFeed.singleton.messages.Count; i++)
            {
                stringBuilder.AppendLine(MessageFeed.singleton.messages[i].message);
            }

            messageLog.text = stringBuilder.ToString();
        }
    }
}
