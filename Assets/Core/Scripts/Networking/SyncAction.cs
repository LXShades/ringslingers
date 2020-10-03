using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SyncActionChain : IMessageBase
{
    public enum ExecutionType
    {
        Predicted,
        Confirmed
    }

    /// <summary>
    /// Currently executing SyncActionChain
    /// </summary>
    public static SyncActionChain executing { get; private set; }

    /// <summary>
    /// Locally-predicted SyncActionChains
    /// </summary>
    private static List<SyncActionChain> predictedChains = new List<SyncActionChain>();

    /// <summary>
    /// Incremental ID to be assigned to the next predictable SyncAction we request
    /// </summary>
    private static byte nextLocalRequestId = 0;

    /// <summary>
    /// Actions within this SyncActionChain
    /// </summary>
    public readonly List<SerializedSyncAction> actions = new List<SerializedSyncAction>();

    /// <summary>
    /// If executing, this is the type of execution taking place (predicted or confirmed)
    /// </summary>
    public ExecutionType currentExecutionType { get; set; }

    /// <summary>
    /// ID of the player calling, or 255 if N/A which shouldn't happen...?
    /// </summary>
    public byte requestingPlayer = 255;

    /// <summary>
    /// ID of this SyncAction request being made, used to match predicted SyncActions
    /// </summary>
    public byte localRequestId = 0;

    /// <summary>
    /// Creates a new SyncActionChain with a single root
    /// </summary>
    public static SyncActionChain Create(SyncAction root, byte requestingPlayer = 255)
    {
        SyncActionChain newChain = new SyncActionChain(); // any magical pooling goes here

        newChain.requestingPlayer = requestingPlayer;
        newChain.localRequestId = nextLocalRequestId++;

        newChain.actions.Add(SerializedSyncAction.FromSyncAction(root));

        return newChain;
    }

    #region Execution and Rewinding
    /// <summary>
    /// Executes this SyncActionChain
    /// </summary>
    public bool Execute(ExecutionType executionType, bool allowParameterRewrites)
    {
        if (SyncActionChain.executing != null)
        {
            Debug.LogError("[SyncActionChain.Execute] Cannot execute SyncActionChain - a chain is already executing, we don't do that!");
            return false;
        }

        Debug.Log($"[SyncActionChain.Execute] Executing {this} with {executionType}");
        currentExecutionType = executionType;

        // Execute the actions
        SyncActionChain.executing = this;
        bool wasSuccessful = false;

        for (int i = 0; i < actions.Count && i < 1 /* we're not ready for multi actions just yet */; i++)
        {
            SyncAction targetAction = actions[i].ApplyToSyncAction();

            try
            {
                if (executionType == ExecutionType.Predicted)
                {
                    if (targetAction.CallPredict())
                    {
                        predictedChains.Add(this);
                        wasSuccessful = true;
                    }
                }
                else
                {
                    targetAction.CallConfirm();
                    wasSuccessful = true;
                }

                if (wasSuccessful && allowParameterRewrites)
                {
                    // reserialize potentially changed parameters
                    actions[i] = SerializedSyncAction.FromSyncAction(targetAction);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SyncActionChain.Execute] Exception during execution of {this}: {e.Message}");
            }
        }

        // Done!
        SyncActionChain.executing = null;

        return wasSuccessful;
    }

    /// <summary>
    /// Rewinds this SyncActionChain
    /// </summary>
    public void Rewind()
    {
        Debug.Log($"[SyncActionChain.Rewind] {this}");

        for (int i = actions.Count - 1; i >= 0; i--)
        {
            SyncAction target = actions[i].ApplyToSyncAction();

            if (target != null)
            {
                target.CallRewind();
            }
            else
            {
                Debug.LogWarning($"[SyncActionChain.Rewind] Failed because target {actions[i].targetId} no longer exists");
            }
        }

        actions.Clear();
    }
    #endregion

    #region Networking
    public static void RegisterHandlers()
    {
        NetworkServer.RegisterHandler<SyncActionChain>(OnServerReceivedSyncActionChain);
        NetworkClient.RegisterHandler<SyncActionChain>(OnClientReceivedSyncActionChain);
    }

    private static void OnClientReceivedSyncActionChain(NetworkConnection conn, SyncActionChain message)
    {
        if (NetworkClient.isLocalClient) // ignore messages sent to the host by the server
            return;

        // If we have a predicted version already, undo it
        SyncActionChain oldChain = FindMatchingPredictedChain(message);

        if (oldChain != null)
        {
            oldChain.Rewind();
        }

        // Server's orders, execute it!
        message.Execute(ExecutionType.Confirmed, false);
    }

    private static void OnServerReceivedSyncActionChain(SyncActionChain message)
    {
        if (message.actions.Count == 1)
        {
            // Received a valid SyncAction. Execute it and send the final result to clients
            message.Execute(ExecutionType.Confirmed, true);

            // Distribute the completed chain to clients
            NetworkServer.SendToAll(message);
        }
    }

    public void Serialize(NetworkWriter writer)
    {
        bool onlyFirstAction = !NetworkServer.active; // server never needs to receive a client's full action chain
        int numActions = Mathf.Clamp(actions.Count, 0, onlyFirstAction ? 1 : 255);

        writer.WriteByte(requestingPlayer);
        writer.WriteByte(localRequestId);
        writer.WriteByte((byte)numActions);

        for (int i = 0; i < numActions; i++)
        {
            writer.WriteInt32(actions[i].targetId);
            writer.WriteBytesAndSizeSegment(actions[i].parameters);
        }
    }

    public void Deserialize(NetworkReader reader)
    {
        try
        {
            requestingPlayer = reader.ReadByte();
            localRequestId = reader.ReadByte();

            int numActions = reader.ReadByte(); // todo check if more than one action was received from a client, we should ignore those

            actions.Clear();
            for (int i = 0; i < numActions; i++)
            {
                actions.Add(new SerializedSyncAction()
                {
                    targetId = reader.ReadInt32(),
                    parameters = reader.ReadBytesAndSizeSegment()
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception deserializing SyncAction: {e.Message}");
            throw e;
        }
    }
    #endregion

    private static SyncActionChain FindMatchingPredictedChain(SyncActionChain confirmedChain)
    {
        if (confirmedChain.requestingPlayer != Netplay.singleton.localPlayerId)
            return null; // only locally requested actions can be reasonably predicted!

        foreach (SyncActionChain chain in predictedChains)
        {
            if (chain.localRequestId == confirmedChain.localRequestId)
                return chain; // we made this request
        }

        return null; // none found
    }

    public override string ToString()
    {
        if (actions.Count > 0 && actions[0].GetType().GetGenericArguments().Length > 0)
        {
            return $"SyncActionChain ({actions.Count} actions / {(actions.Count > 0 ? $"{actions[0].GetType().Name}<{actions[0].GetType().GetGenericArguments()[0].Name}>" : "empty")})";
        }
        else
        {
            return $"SyncActionChain ({actions.Count} actions)";
        }
    }
}

public class SyncAction
{
    public static readonly Dictionary<int, SyncAction> syncActionFromHash = new Dictionary<int, SyncAction>();

    public readonly GameObject owner;

    public int targetId { get; private set; }

    protected SyncAction() { }

    protected SyncAction(GameObject owner, int indentifierHash)
    {
        this.owner = owner;
        this.targetId = indentifierHash;
        syncActionFromHash[targetId] = this;
    }

    public static int CreateIdHash(GameObject owner, Delegate onConfirm, Delegate onPredict, Delegate onRewind)
    {
        uint netId = 0;

        if (owner)
        {
            NetworkIdentity identity = owner.GetComponent<NetworkIdentity>();
            if (identity)
            {
                netId = identity.netId;
            }
        }

        return $"{netId}/{onConfirm.Method.DeclaringType}/{onConfirm.Method.Name}/{onPredict.Method.Name}/{onRewind.Method.Name}".GetHashCode();
    }

    public static SyncAction FindSyncActionFromTargetId(int hash)
    {
        SyncAction action;

        if (syncActionFromHash.TryGetValue(hash, out action))
        {
            return action;
        }
        else
        {
            return null;
        }
    }

    public virtual void CallConfirm() { }

    public virtual bool CallPredict() { return true; }

    public virtual void CallRewind() { }

    public virtual void SerializeParameters(NetworkWriter writer) { }

    public virtual void DeserializeParameters(NetworkReader reader) { }
}

public class SyncAction<TParams> : SyncAction 
    where TParams : struct, IMessageBase
{
    public TParams parameters;

    public delegate void CallDelegate(ref TParams parameters);
    public delegate bool BoolDelegate(ref TParams parameters);

    public CallDelegate onConfirm;
    public BoolDelegate onPredict;
    public CallDelegate onRewind;

    protected SyncAction(GameObject owner, int typeHash) : base(owner, typeHash) { }

    public static SyncAction<TParams> Register(GameObject owner, CallDelegate onConfirm, BoolDelegate onPredict, CallDelegate onRewind)
    {
        return new SyncAction<TParams>(owner, CreateIdHash(owner, onConfirm, onPredict, onRewind))
        {
            onConfirm = onConfirm,
            onPredict = onPredict,
            onRewind = onRewind
        };
    }

    public bool Request(TParams inputParameters)
    {
        parameters = inputParameters;

        if (SyncActionChain.executing == null)
        {
            // create and execute a new requested SyncActionChain
            SyncActionChain chainToExecute = SyncActionChain.Create(this, (byte)Netplay.singleton.localPlayerId);
            bool wasExecutionSuccessful = chainToExecute.Execute(SyncActionChain.ExecutionType.Predicted, true);

            if (wasExecutionSuccessful)
            {
                // request the action on the server
                if (NetworkClient.isConnected)
                {
                    NetworkClient.Send<SyncActionChain>(chainToExecute);
                }
                else if (NetworkServer.active)
                {
                    NetworkServer.SendToAll<SyncActionChain>(chainToExecute);
                }
            }
            else
            {
                chainToExecute.Rewind();
            }
            return true;
        }
        else
        {
            // execute immediately because we're already in a chain
            switch (SyncActionChain.executing.currentExecutionType)
            {
                case SyncActionChain.ExecutionType.Confirmed:
                    onConfirm?.Invoke(ref parameters);
                    break;
                case SyncActionChain.ExecutionType.Predicted:
                    onPredict?.Invoke(ref parameters);
                    break;
            }

            SyncActionChain.executing.actions.Add(SerializedSyncAction.FromSyncAction(this)); // todo: confirmed ones should swap in as before
            return true;
        }
    }

    public override void CallConfirm() => onConfirm?.Invoke(ref parameters);

    public override bool CallPredict() => onPredict != null ? onPredict.Invoke(ref parameters) : true;

    public override void CallRewind() => onRewind?.Invoke(ref parameters);

    public override void SerializeParameters(NetworkWriter writer)
    {
        parameters.Serialize(writer);
    }

    public override void DeserializeParameters(NetworkReader reader)
    {
        parameters.Deserialize(reader);
    }

    public override string ToString()
    {
        return GetType().Name;
    }
}

/// <summary>
/// This is an action that 
/// </summary>
public struct SerializedSyncAction
{
    public int targetId;
    public ArraySegment<byte> parameters;

    public static SerializedSyncAction FromSyncAction(SyncAction source)
    {
        SerializedSyncAction serializedAction = new SerializedSyncAction();

        serializedAction.targetId = source.targetId;

        using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
        {
            source.SerializeParameters(writer);
            serializedAction.parameters = new ArraySegment<byte>(writer.ToArray());
        }

        return serializedAction;
    }

    /// <summary>
    /// Applies parameters to a syncaction and returns the syncaction
    /// </summary>
    public SyncAction ApplyToSyncAction()
    {
        SyncAction target = SyncAction.FindSyncActionFromTargetId(targetId);

        if (target != null)
        {
            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(parameters))
            {
                target.DeserializeParameters(reader);
            }

            return target;
        }

        return null;
    }
}