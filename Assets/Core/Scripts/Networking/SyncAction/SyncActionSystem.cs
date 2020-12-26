using Mirror;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class SyncActionSystem
{
    public enum FunctionType
    {
        Confirm,
        Predict,
        Rewind,
        Reject
    }

    public static readonly Dictionary<uint, ISyncAction> syncActionFromTargetId = new Dictionary<uint, ISyncAction>();
    private static readonly Dictionary<System.Type, uint> syncActionIndexByType = new Dictionary<System.Type, uint>();
    private static readonly Dictionary<uint, System.Type> syncActionTypeByIndex = new Dictionary<uint, System.Type>();

    public static int RegisterSyncActions(GameObject owner, bool identityless = false)
    {
        if (!owner.TryGetComponent(out NetworkIdentity identity) && !identityless)
        {
            Log.WriteError($"Cannot register a SyncAction on an object \"{owner}\" with no network identity");
            return 0;
        }

        if (identity && !identity.isServer && !identity.isClient)
        {
            Log.WriteWarning($"Not registering SyncActions on uninitialized network object \"{owner}\"");
            return 0;
        }

        int numRegistered = 0;
        foreach (ISyncAction syncActionComponent in owner.GetComponents<ISyncAction>())
        {
            // this only returns components that include an ISyncAction
            // we still need to cast it to the valid syncaction types
            foreach (var syncAction in syncActionComponent.GetType().GetInterfaces())
            {
                if (!typeof(ISyncAction).IsAssignableFrom(syncAction) || !syncAction.IsGenericType)
                    continue;

                uint targetId = GenerateTargetId(owner, syncAction);

                if (syncActionFromTargetId.TryGetValue(targetId, out ISyncAction value))
                {
                    if (value != null)
                    {
                        Log.WriteError($"A SyncAction with a duplicate ID  {targetId} was created by {owner} (netID {owner.GetComponent<NetworkIdentity>()?.netId})! Bad ID/non-networked object?");
                        continue;
                    }
                }

                syncActionFromTargetId[targetId] = (ISyncAction)typeof(SyncActionSystem).GetMethod(nameof(ExtractSyncActionInterface)).MakeGenericMethod(new Type[] { syncAction }).Invoke(null, new object[] { syncActionComponent });
                numRegistered++;
            }
        }

        if (numRegistered > 0)
        {
            Log.Write($"Registered {numRegistered} SyncActions on {owner}");
        }

        return numRegistered;
    }

    public static ISyncAction ExtractSyncActionInterface<TParameter>(object target)
        where TParameter : class, ISyncAction
    {
        return target as TParameter;
    }

    public static uint GenerateTargetId<TSyncAction, TSyncActionParams>(TSyncAction target)
        where TSyncAction : NetworkBehaviour, ISyncAction<TSyncActionParams>
    {
        uint netId = target != null && target.netIdentity != null ? target.netIdentity.netId : 0;

        return (netId & 0xFFFF) | (syncActionIndexByType[typeof(ISyncAction<TSyncActionParams>)] << 16);
    }

    public static uint GenerateTargetId<TSyncActionParams>(NetworkBehaviour targetBehaviour, ISyncAction<TSyncActionParams> targetSyncAction)
    {
        uint netId = targetBehaviour != null && targetBehaviour.netIdentity != null ? targetBehaviour.netIdentity.netId : 0;

        return (netId & 0xFFFF) | (syncActionIndexByType[typeof(ISyncAction<TSyncActionParams>)] << 16);
    }

    public static uint GenerateTargetId(GameObject owner, System.Type syncActionType)
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

        return (netId & 0xFFFF) | (syncActionIndexByType[syncActionType] << 16);
    }

    // Gets the specific ISyncAction<T> type from a target ID
    public static Type GetTargetIdType(uint targetId)
    {
        return syncActionTypeByIndex[targetId >> 16];
    }

    // non-ref version
    public static bool Request<TSyncActionParams>(ISyncAction<TSyncActionParams> target, TSyncActionParams parameters)
        where TSyncActionParams : NetworkMessage
    {
        return Request(target, ref parameters);
    }

    public static bool Request<TSyncActionParams>(ISyncAction<TSyncActionParams> target, ref TSyncActionParams parameters)
        where TSyncActionParams : NetworkMessage
    {
        if (SyncActionChain.executing == null)
        {
            // create and execute a new requested SyncActionChain
            SyncActionChain chainToExecute = SyncActionChain.Create(target, in parameters, (byte)Netplay.singleton.localPlayerId);
            bool wasExecutionSuccessful = chainToExecute.Execute(SyncActionChain.ExecutionType.Predicting);

            if (wasExecutionSuccessful)
            {
                // request the action on the server
                if (NetworkClient.isConnected)
                {
                    NetworkClient.Send<SerializedSyncActionChain>(chainToExecute.Serialize());
                }
                else if (NetworkServer.active)
                {
                    NetworkServer.SendToAll<SerializedSyncActionChain>(chainToExecute.Serialize());
                }

                return true;
            }
            else
            {
                chainToExecute.Rewind(false);
                return false;
            }
        }
        else
        {
            SyncAction<TSyncActionParams> syncAction = new SyncAction<TSyncActionParams>() { target = target, parameters = default };

            // execute immediately because we're already in a chain
            switch (SyncActionChain.executing.currentExecutionType)
            {
                case SyncActionChain.ExecutionType.Confirming:
                    target.OnConfirm(SyncActionChain.executing, ref parameters);
                    syncAction.parameters = parameters;
                    SyncActionChain.executing.actions.Add(syncAction.Box());
                    return true;
                case SyncActionChain.ExecutionType.Reconfirming:
                    if (SyncActionChain.executing.PopNextAction(out SyncActionBox boxedAction))
                    {
                        if (boxedAction.CanCall())
                        {
                            if (boxedAction.GetParameterType() == typeof(TSyncActionParams) || (ISyncAction<TSyncActionParams>)boxedAction.GetTarget() != target)
                            {
                                boxedAction.Call(SyncActionChain.executing, FunctionType.Confirm, true);
                            }
                            else
                            {
                                Log.WriteError($"SyncAction flow disrupted: requested target/type {target}/{typeof(TSyncActionParams).Name} mismatches confirmed chain {boxedAction.GetTarget()}/{boxedAction.GetParameterType().Name}");
                            }
                        }
                        else
                        {
                            Log.WriteError($"SyncAction flow disrupted: confirmed action in chain is uncallable (invalid target likely)");
                        }
                    }
                    break;
                case SyncActionChain.ExecutionType.Predicting:
                    target.OnPredict(SyncActionChain.executing, ref parameters);
                    syncAction.parameters = parameters;
                    SyncActionChain.executing.actions.Add(syncAction.Box());
                    break;
            }

            return true;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GenerateSyncActionCaches();
    }

    private static void GenerateSyncActionCaches()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        List<System.Type> typesAlphabetical = new List<System.Type>();

        foreach (Type type in assembly.GetTypes())
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition().Equals(typeof(ISyncAction<>)))
                {
                    if (!typesAlphabetical.Contains(iface))
                    {
                        typesAlphabetical.Add(iface);
                    }
                }
            }
        }

        typesAlphabetical.Sort((a, b) => String.Compare(a.GetGenericArguments()[0].FullName, b.GetGenericArguments()[0].FullName));

        for (int i = 0; i < typesAlphabetical.Count; i++)
        {
            syncActionIndexByType[typesAlphabetical[i]] = (uint)i;
            syncActionTypeByIndex[(uint)i] = typesAlphabetical[i];
            //syncActionGenericCallers[typesAlphabetical[i]] = Delegate.CreateDelegate(typeof(SyncActionSystem).GetMethod(nameof(CallGeneric)).MakeGenericMethod(typesAlphabetical[i]));
        }
    }
}

public struct SerializedSyncAction : NetworkMessage
{
    public uint targetId;
    public ArraySegment<byte> parameters;

    public bool Deserialize<TParameters>(out SyncAction<TParameters> output)
        where TParameters : NetworkMessage
    {
        ISyncAction actionTarget;
        SyncActionSystem.syncActionFromTargetId.TryGetValue(targetId, out actionTarget);

        Debug.Assert(actionTarget.GetType() == typeof(ISyncAction<TParameters>));

        output = new SyncAction<TParameters>();
        if (actionTarget != null)
        {
            if (actionTarget is ISyncAction<TParameters> actionTargetTyped)
            {
                using (PooledNetworkReader reader = NetworkReaderPool.GetReader(this.parameters))
                {
                    output.parameters = reader.Read<TParameters>();
                    output.target = actionTargetTyped;
                    return true;
                }
            }
            else
            {
                Log.WriteWarning($"Requested SyncAction type {typeof(ISyncAction<TParameters>).Name} does not match actual target {actionTarget.GetType()}");
            }
        }
        else
        {
            Log.WriteWarning($"SyncAction target {targetId} could not be found");
        }

        return false;
    }

    public SyncActionBox DeserializeAsBox()
    {
        SyncActionSystem.syncActionFromTargetId.TryGetValue(targetId, out ISyncAction target);

        if (target == null)
        {
            Log.WriteWarning($"Target is null");
            return null;
        }

        // Call Box which will create a SyncActionBox<TParameters> where TParameters is the parameter type of our target
        MethodInfo box = typeof(SerializedSyncAction).GetMethod(nameof(Box), BindingFlags.NonPublic | BindingFlags.Static);
        Type[] parameterTypes = SyncActionSystem.GetTargetIdType(targetId).GenericTypeArguments;
        MethodInfo genericBox = box.MakeGenericMethod(parameterTypes);
        return (SyncActionBox)genericBox.Invoke(null, new object[] { this });
    }

    private static SyncActionBox Box<TParameters>(SerializedSyncAction serializedAction)
        where TParameters : NetworkMessage
    {
        SyncActionSystem.syncActionFromTargetId.TryGetValue(serializedAction.targetId, out ISyncAction target);

        using (PooledNetworkReader reader = NetworkReaderPool.GetReader(serializedAction.parameters))
        {
            return new SyncActionBox<TParameters>()
            {
                action = new SyncAction<TParameters>()
                {
                    parameters = reader.Read<TParameters>(),
                    target = (ISyncAction<TParameters>)target
                }
            };
        }
    }
}

public struct SyncAction<TParameters>
    where TParameters : NetworkMessage
{
    public ISyncAction<TParameters> target;
    public TParameters parameters;

    public SyncAction(ISyncAction<TParameters> target, TParameters parameters)
    {
        this.target = target;
        this.parameters = parameters;
    }

    public SerializedSyncAction Serialize()
    {
        SerializedSyncAction serializedAction = new SerializedSyncAction();

        serializedAction.targetId = SyncActionSystem.GenerateTargetId(target as NetworkBehaviour, target);

        using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
        {
            writer.Write(parameters);
            serializedAction.parameters = new ArraySegment<byte>(writer.ToArray());
        }

        return serializedAction;
    }

    public bool Call(SyncActionChain chain, SyncActionSystem.FunctionType function, bool replaceParameters)
    {
        if (replaceParameters)
        {
            switch (function)
            {
                case SyncActionSystem.FunctionType.Confirm: 
                    return target.OnConfirm(chain, ref parameters);
                case SyncActionSystem.FunctionType.Predict:
                    return target.OnPredict(chain, ref parameters);
                case SyncActionSystem.FunctionType.Rewind:
                    target.OnRewind(chain, ref parameters, true);
                    return true;
                case SyncActionSystem.FunctionType.Reject:
                    target.OnRewind(chain, ref parameters, false);
                    return true;
            }
        }
        else
        {
            TParameters paramCopy = parameters;

            switch (function)
            {
                case SyncActionSystem.FunctionType.Confirm:
                    return target.OnConfirm(chain, ref paramCopy);
                case SyncActionSystem.FunctionType.Predict:
                    return target.OnPredict(chain, ref paramCopy);
                case SyncActionSystem.FunctionType.Rewind:
                    target.OnRewind(chain, ref paramCopy, true);
                    return true;
                case SyncActionSystem.FunctionType.Reject:
                    target.OnRewind(chain, ref paramCopy, false);
                    return true;
            }
        }
        return false; // should never happen, should also compile.
    }

    public SyncActionBox Box()
    {
        return new SyncActionBox<TParameters>() { action = this };
    }
}

public class SyncActionBox
{
    public virtual bool Call(SyncActionChain chain, SyncActionSystem.FunctionType function, bool replaceParameters)
    {
        Log.WriteWarning("SyncActionBox.Call was called on an untyped SyncActionBox...");
        return false;
    }

    public virtual SerializedSyncAction Serialize() => new SerializedSyncAction();

    public virtual bool CanCall() => false;

    public virtual object GetTarget() => null;

    public virtual Type GetParameterType() => null;
}

public class SyncActionBox<T> : SyncActionBox
    where T : NetworkMessage
{
    public SyncAction<T> action;

    public override bool Call(SyncActionChain chain, SyncActionSystem.FunctionType function, bool replaceParameters) => action.Call(chain, function, replaceParameters);

    public override SerializedSyncAction Serialize() => action.Serialize();

    public override bool CanCall() => action.target != null;

    public override Type GetParameterType() => typeof(T);

    public override object GetTarget() => action.target;
}