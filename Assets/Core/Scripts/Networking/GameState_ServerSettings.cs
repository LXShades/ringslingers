using Mirror;
using UnityEngine;

public class GameState_ServerSettings : GameStateComponent
{
    public const float maxHitLagCompensation = 0.2f;

    public struct Settings
    {
        public float hitLagCompensation;

        public static Settings Default => new Settings()
        {
            hitLagCompensation = 0f
        };
    }

    [SyncVar]
    public Settings settings = Settings.Default;

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
        {
            settings = new Settings()
            {
                hitLagCompensation = Mathf.Clamp(GamePreferences.serverRewindTolerance, 0f, maxHitLagCompensation)
            };
        }
    }
}
