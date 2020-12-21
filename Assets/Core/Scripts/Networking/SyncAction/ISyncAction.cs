// using Nothing; omg

public interface ISyncAction
{
}

public interface ISyncAction<TSyncActionParams> : ISyncAction
{
    bool OnConfirm(SyncActionChain chain, ref TSyncActionParams parameters);
    bool OnPredict(SyncActionChain chain, ref TSyncActionParams parameters);
    void OnRewind(SyncActionChain chain, ref TSyncActionParams parameters);
}