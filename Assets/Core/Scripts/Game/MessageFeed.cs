using Mirror;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class MessageFeed: NetworkBehaviour
{
    public static MessageFeed singleton { get; private set; }

    public struct LogMessage
    {
        public float postTime;
        public string message;
    }

    public List<LogMessage> messages { private set; get; } = new List<LogMessage>();

    public int messageHistoryLength = 10;

    public float lastPostedMessageTime;

    public Action<string> onNewMessage;

    private void Awake()
    {
        singleton = this;
    }

    /// <summary>
    /// Posts a message to the message log and distributes it to clients. Server-side only
    /// </summary>
    /// <param name="message"></param>
    public static void Post(string message)
    {
        singleton?.PostImpl(message);
    }

    /// <summary>
    /// Posts a message to the message locally (not networked)
    /// </summary>
    /// <param name="message"></param>
    public static void PostLocal(string message)
    {
        singleton?.PostLocalImpl(message);
    }

    [Server]
    private void PostImpl(string message)
    {
        Log.Write($"{message}");

        RpcPost(message);
    }

    private void PostLocalImpl(string message)
    {
        // Format the message
        StringBuilder sb = new StringBuilder(message);

        if (Netplay.singleton.localPlayer)
        {
            sb.Replace($"<player>{Netplay.singleton.localPlayer.playerName}</player>", $"<color=yellow>{Netplay.singleton.localPlayer.playerName}</color>");
        }

        sb.Replace("<player>", "<color=red><noparse>");
        sb.Replace("</player>", "</noparse></color>");
        sb.Replace("<localevent>", "<color=blue>");
        sb.Replace("</localevent>", "</color>");

        // add it
        messages.Add(new LogMessage()
        {
            message = sb.ToString(),
            postTime = Time.time
        });

        // remove old messages
        while (messages.Count > messageHistoryLength)
            messages.RemoveAt(0);

        lastPostedMessageTime = Time.time;
        onNewMessage?.Invoke(message);
    }

    // broadcast messages to clients
    [ClientRpc]
    public void RpcPost(string message)
    {
        PostLocal(message);
    }
}
