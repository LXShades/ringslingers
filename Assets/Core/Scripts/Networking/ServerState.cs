using Mirror;
using UnityEngine;

public class ServerState : ServerStateBase
{
    public new static ServerState instance => ServerStateBase.instance as ServerState;

    public const float maxServerRewindTolerance = 0.2f;

    [SyncVar]
    public float serverRewindTolerance;

    public override void OnStartServer()
    {
        base.OnStartServer();

        GamePreferences.onPreferencesChanged += OnGamePreferencesChanged;
        OnGamePreferencesChanged();
    }

    void OnDestroy()
    {
        GamePreferences.onPreferencesChanged -= OnGamePreferencesChanged;
    }

    private void OnGamePreferencesChanged()
    {
        if (isServer)
            serverRewindTolerance = Mathf.Clamp(GamePreferences.serverRewindTolerance, 0f, maxServerRewindTolerance);
    }
}
