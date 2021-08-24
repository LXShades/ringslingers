using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NetDebugUI : MonoBehaviour
{
    public struct Stat
    {
        public int total
        {
            set
            {
                if (value > _total)
                    nextPerSecond += value - _total;

                _total = value;
            }
            get => _total;
        }
        public int perSecond { get; private set; }

        private int nextPerSecond;

        private int _total;
        private float lastPerSecondSnapshot;

        public void Add(int amountToAdd)
        {
            total += amountToAdd;
            nextPerSecond += amountToAdd;

            RefreshPerSecondStats();
        }

        private void RefreshPerSecondStats()
        {
            if (Time.unscaledTime - lastPerSecondSnapshot >= 1f)
            {
                perSecond = nextPerSecond;
                nextPerSecond = 0;

                lastPerSecondSnapshot = Time.unscaledTime;
            }
        }
    }

    private class Stats
    {
        public Stat bytesIn;
        public Stat countIn;
        public Stat bytesOut;
        public Stat countOut;

        public void Apply(Mirror.NetworkDiagnostics.MessageInfo obj, bool isIn)
        {
            if (isIn)
            {
                bytesIn.total += (obj.bytes + 40) * obj.count;
                countIn.total += obj.count;
            }
            else
            {
                bytesOut.total += (obj.bytes + 40) * obj.count;
                countOut.total += obj.count;
            }
        }

        public override string ToString()
        {
            return $"{ToByteUnit(bytesIn.total)}/{ToByteUnit(bytesOut.total)} {ToUnit(countIn.total)}/{ToUnit(countOut.total)}";
        }
    }

    public Text textbox;
    private Stats totalStats = new Stats();

    private Dictionary<string, Stats> statsByCategory = new Dictionary<string, Stats>();
    private Dictionary<System.Guid, Stats> statsByObjectType = new Dictionary<System.Guid, Stats>();
    private Dictionary<System.Guid, string> objectNameByAssetId = new Dictionary<System.Guid, string>();

    public void OnEnable()
    {
        NetworkDiagnostics.InMessageEvent += OnInMessage;
        NetworkDiagnostics.OutMessageEvent += OnOutMessage;
    }

    public void OnDisable()
    {
        NetworkDiagnostics.InMessageEvent -= OnInMessage;
        NetworkDiagnostics.OutMessageEvent -= OnOutMessage;
    }

    void Update()
    {
        textbox.text = $"{totalStats}\n";

        foreach (KeyValuePair<string, Stats> statKeyVal in statsByCategory)
        {
            textbox.text += $"{statKeyVal.Key}: {statKeyVal.Value}\n";
        }

        foreach (KeyValuePair<System.Guid, Stats> statKeyVal in statsByObjectType)
        {
            if (statKeyVal.Key == null)
            {
                continue;
            }

            textbox.text += $"{objectNameByAssetId[statKeyVal.Key]}: {statKeyVal.Value}\n";
        }
    }

    private void OnInMessage(NetworkDiagnostics.MessageInfo msg) => OnMessageEvent(msg, true);
    private void OnOutMessage(NetworkDiagnostics.MessageInfo msg) => OnMessageEvent(msg, false);

    private void OnMessageEvent(NetworkDiagnostics.MessageInfo msg, bool isInbound)
    {
        string msgType = msg.message.GetType().Name;

        if (!statsByCategory.ContainsKey(msgType))
        {
            statsByCategory.Add(msgType, new Stats());
        }
        statsByCategory[msgType].Apply(msg, isInbound);

        NetworkIdentity identity = null;
        if (msg.message is EntityStateMessage messageAsUpdateVars)
        {
            NetworkIdentity.spawned.TryGetValue(messageAsUpdateVars.netId, out identity);
        }
        if (msg.message is CommandMessage messageAsCommand)
        {
            NetworkIdentity.spawned.TryGetValue(messageAsCommand.netId, out identity);
        }
        if (msg.message is RpcMessage messageAsRpc)
        {
            NetworkIdentity.spawned.TryGetValue(messageAsRpc.netId, out identity);
        }

        if (identity)
        {
            if (!statsByObjectType.ContainsKey(identity.assetId))
            {
                statsByObjectType.Add(identity.assetId, new Stats());
                objectNameByAssetId.Add(identity.assetId, identity.gameObject.name);
            }

            statsByObjectType[identity.assetId].Apply(msg, isInbound);
        }

        totalStats.Apply(msg, isInbound);
    }

    static string ToByteUnit(int numBytes)
    {
        if (numBytes >= 1024 * 1024)
            return $"{((float)numBytes / 1024f / 1024f).ToString("#.0")}MB";
        if (numBytes >= 1024)
            return $"{((float)numBytes / 1024f).ToString("#.0")}KB";
        return $"{numBytes}B";
    }

    static string ToUnit(int num)
    {
        if (num >= 1000000)
            return $"{((float)num / 1000000f).ToString("#.0")}M";
        if (num >= 1000)
            return $"{((float)num / 1000f).ToString("#.0")}K";
        return num.ToString();
    }
}
