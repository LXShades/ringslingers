using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SyncActionChain
{
    public enum ExecutionType
    {
        /// <summary>
        /// Player is locally predicting the action chain. This generates the chain out of predictable actions as it progresses
        /// </summary>
        Predicting,

        /// <summary>
        /// Player is playing back the sequence of actions confirmed by the server. Action parameters are susbtituted from the chain for every action executed
        /// </summary>
        Reconfirming,

        /// <summary>
        /// Server is confirming the action chain. This generates the chain and sends the result to clients
        /// </summary>
        Confirming
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
    /// SyncActionChains that have been received and not yet processed
    /// </summary>
    private static List<SyncActionChain> receivedChains = new List<SyncActionChain>();

    /// <summary>
    /// Incremental ID to be assigned to the next predictable SyncAction we request
    /// </summary>
    private static byte nextLocalRequestId = 0;

    /// <summary>
    /// Actions within this SyncActionChain
    /// </summary>
    public List<SyncActionBox> actions = new List<SyncActionBox>();

    /// <summary>
    /// If executing, this is the type of execution taking place (predicted or confirmed)
    /// </summary>
    public ExecutionType currentExecutionType { get; set; }

    /// <summary>
    /// ID of the player calling, or 255 if N/A which shouldn't happen...?
    /// </summary>
    public byte sourcePlayer = 255;

    /// <summary>
    /// ID of this SyncAction request being made, used to match predicted SyncActions
    /// </summary>
    public byte requestId = 0;

    /// <summary>
    /// Time.unscaledTime that this action was created
    /// </summary>
    public float requestTime { get; private set; }

    /// <summary>
    /// How much time in seconds since this chain was requested
    /// </summary>
    public float timeSinceRequest => Time.unscaledTime - requestTime;

    public bool isLocalRequest => sourcePlayer == Netplay.singleton.localPlayerId;

    public const float syncActionExpiryTime = 0.5f;

    private int nextExecutableAction = 0;

    /// <summary>
    /// Creates a new SyncActionChain with a single root
    /// </summary>
    public static SyncActionChain Create(SyncActionBox root, byte requestingPlayer = 255)
    {
        SyncActionChain newChain = new SyncActionChain(); // any magical pooling goes here

        newChain.sourcePlayer = requestingPlayer;
        newChain.requestId = nextLocalRequestId++;
        newChain.requestTime = Time.unscaledTime;

        newChain.actions.Add(root);

        return newChain;
    }

    /// <summary>
    /// Creates a new SyncActionChain with a single root
    /// </summary>
    public static SyncActionChain Create<TSyncActionParams>(ISyncAction<TSyncActionParams> rootTarget, in TSyncActionParams rootParameters, byte requestingPlayer = 255)
        where TSyncActionParams : NetworkMessage
    {
        SyncActionChain newChain = new SyncActionChain(); // any magical pooling goes here

        newChain.sourcePlayer = requestingPlayer;
        newChain.requestId = nextLocalRequestId++;
        newChain.requestTime = Time.unscaledTime;

        newChain.actions.Add(new SyncAction<TSyncActionParams>(rootTarget, rootParameters).Box());

        return newChain;
    }

    public static void Tick()
    {
        // Execute received SyncActionChains
        foreach (SyncActionChain chain in receivedChains)
        {
            // If we have a predicted version already, undo it
            // (currently even the host has predicted versions, for consistency and testing)
            SyncActionChain oldChain = FindMatchingPredictedChain(chain);

            if (oldChain != null)
            {
                chain.requestTime = oldChain.requestTime; // transfer RTT to confirmation
                oldChain.RewindAndRemove(true);
            }

            chain.requestTime = Time.unscaledTime; // todo: maybe estimate RTT?

            // Execute the SyncActionChain
            if (NetworkServer.active)
            {
                if (chain.Execute(ExecutionType.Confirming))
                {
                    // Distribute the completed chain to clients
                    NetworkServer.SendToAll(chain.Serialize());
                }
            }
            else
            {
                chain.Execute(ExecutionType.Reconfirming);
            }
        }

        receivedChains.Clear();

        // Remove expired SyncActionChains
        for (int i = 0; i < predictedChains.Count; i++)
        {
            if (predictedChains[i].timeSinceRequest > syncActionExpiryTime)
            {
                Log.Write($"Predicted chain {predictedChains[i]} expired.");

                // expired
                predictedChains[i].RewindAndRemove(false);
            }
        }
    }

    #region Execution and Rewinding
    /// <summary>
    /// Executes this SyncActionChain
    /// </summary>
    public bool Execute(ExecutionType executionType)
    {
        if (SyncActionChain.executing != null)
        {
            Log.WriteError("Cannot execute SyncActionChain - a chain is already executing, we don't do that!");
            return false;
        }

        bool wasSuccessful = false;

        Log.Write($"Executing {this} with {executionType}");

        SyncActionChain.executing = this;
        nextExecutableAction = 1;
        currentExecutionType = executionType;

        try
        {
            // Execute the actions
            if (executionType == ExecutionType.Predicting)
            {
                // we're predicting this locally, which should result in a new chain that we can add to our predicted chain record
                if (actions[0].Call(this, SyncActionSystem.FunctionType.Predict, true))
                {
                    predictedChains.Add(this);
                    wasSuccessful = true;
                }
            }
            else if (executionType == ExecutionType.Confirming || executionType == ExecutionType.Reconfirming)
            {
                if (actions[0] != null)
                {
                    // we're confirming this action, either on the server or client
                    wasSuccessful = actions[0].Call(this, SyncActionSystem.FunctionType.Confirm, executionType == ExecutionType.Confirming);
                }
                else
                {
                    Log.WriteError("Chain action 0 is null! Invalid chain.");
                }
            }
        }
        catch (Exception e)
        {
            Log.WriteException(e);
        }

        // Done!
        SyncActionChain.executing = null;

        return wasSuccessful;
    }

    /// <summary>
    /// Rewinds this SyncActionChain
    /// </summary>
    public void Rewind(bool isConfirming)
    {
        Log.Write($"{this}");

        for (int i = actions.Count - 1; i >= 0; i--)
        {
            actions[i].Call(this, isConfirming ? SyncActionSystem.FunctionType.Rewind : SyncActionSystem.FunctionType.Reject, true);
        }

        actions.Clear();
    }

    /// <summary>
    /// Rewinds this SyncActionChain and, if predicted, removes from the predicted action chain list
    /// </summary>
    public void RewindAndRemove(bool isConfirming)
    {
        Rewind(isConfirming);
        predictedChains.Remove(this);
    }

    public bool PopNextAction(out SyncActionBox actionOut)
    {
        if (nextExecutableAction < actions.Count)
        {
            actionOut = actions[nextExecutableAction++];
            return true;
        }
        else
        {
            actionOut = null;
            return false;
        }
    }
    #endregion

    #region Networking
    public SerializedSyncActionChain Serialize()
    {
        return new SerializedSyncActionChain() { sourcePlayer = sourcePlayer, actions = actions, requestId = requestId };
    }

    public static void RegisterHandlers()
    {
        NetworkServer.RegisterHandler<SerializedSyncActionChain>(OnServerReceivedSyncActionChain);
        NetworkClient.RegisterHandler<SerializedSyncActionChain>(OnClientReceivedSyncActionChain);
    }

    private static void OnClientReceivedSyncActionChain(SerializedSyncActionChain message)
    {
        if (NetworkClient.isLocalClient) // ignore messages sent to the host by the host
            return;

        receivedChains.Add(message.Deserialize());
    }

    private static void OnServerReceivedSyncActionChain(NetworkConnection conn, SerializedSyncActionChain message)
    {
        SyncActionChain receivedChain = message.Deserialize();

        receivedChain.sourcePlayer = (byte)Netplay.singleton.GetPlayerIdFromConnectionId(conn.connectionId);

        receivedChains.Add(receivedChain);
    }
    #endregion

    #region Misc
    private static SyncActionChain FindMatchingPredictedChain(SyncActionChain confirmedChain)
    {
        if (confirmedChain.sourcePlayer != Netplay.singleton.localPlayerId)
            return null; // only locally requested actions can be reasonably predicted!

        foreach (SyncActionChain chain in predictedChains)
        {
            if (chain.requestId == confirmedChain.requestId)
                return chain; // we made this request
        }

        return null; // none found
    }

    public override string ToString()
    {
        if (actions.Count > 0)
        {
            if (actions[0] != null && actions[0].GetType().GetGenericArguments().Length > 0)
            {
                return $"SyncActionChain ({actions.Count} actions / {(actions.Count > 0 ? $"{actions[0].GetType().Name}<{actions[0].GetType().GetGenericArguments()[0].Name}>)" : "empty")})";
            }
            else
            {
                return $"SyncActionChain ({actions.Count} actions / <INVALID FIRST ACTION>)";
            }
        }
        else
        {
            return $"SyncActionChain ({actions.Count} actions)";
        }
    }
    #endregion
}

/// <summary>
/// SyncActionChain that can be sent or received as a message
/// </summary>
public struct SerializedSyncActionChain : NetworkMessage
{
    public byte sourcePlayer;
    public byte requestId;
    public List<SyncActionBox> actions;

    public SyncActionChain Deserialize()
    {
        return new SyncActionChain() { actions = actions, sourcePlayer = sourcePlayer, requestId = requestId };
    }
}

public static class SyncActionChainReaderWriter
{
    public static void WriteSyncActionChain(this NetworkWriter writer, SerializedSyncActionChain chain)
    {
        bool onlyFirstAction = !NetworkServer.active; // server never needs to receive a client's full action chain
        int numActions = Mathf.Clamp(chain.actions.Count, 0, onlyFirstAction ? 1 : 255);

        writer.WriteByte(chain.sourcePlayer);
        writer.WriteByte(chain.requestId);
        writer.WriteByte((byte)numActions);

        for (int i = 0; i < numActions; i++)
        {
            writer.Write(chain.actions[i].Serialize());
        }
    }

    public static SerializedSyncActionChain ReadSyncActionChain(this NetworkReader reader)
    {
        try
        {
            SerializedSyncActionChain chain = new SerializedSyncActionChain();
            chain.sourcePlayer = reader.ReadByte();
            chain.requestId = reader.ReadByte();
            chain.actions = new List<SyncActionBox>();

            int numActions = reader.ReadByte(); // todo check if more than one action was received from a client, we should ignore those

            for (int i = 0; i < numActions; i++)
            {
                SerializedSyncAction serializedAction = reader.Read<SerializedSyncAction>();
                chain.actions.Add(serializedAction.DeserializeAsBox());
            }

            return chain;
        }
        catch (Exception e)
        {
            Log.WriteError($"Exception deserializing SyncAction: {e.Message}");
            throw e;
        }
    }
}