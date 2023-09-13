using Mirror;
using System;
using System.Linq;
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

    // Networks the mods that get added/loaded (ModManager isn't a persistent networked object atm, so we do it here)
    public readonly SyncList<RingslingersMod> addedMods = new SyncList<RingslingersMod>();

    private bool clientModsNeedRechecking = false;

    public override void OnStartServer()
    {
        base.OnStartServer();

        ServerInitMods();
        ModManager.onAnyModLoaded += OnModLoaded;

        GamePreferences.onPreferencesChanged += OnGamePreferencesChanged;
        OnGamePreferencesChanged();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!NetworkServer.active)
            addedMods.Callback += ClientOnAddedModsCallback;
    }

    void OnDestroy()
    {
        GamePreferences.onPreferencesChanged -= OnGamePreferencesChanged;
        ModManager.onAnyModLoaded -= OnModLoaded;
    }

    private void Update()
    {
        if (clientModsNeedRechecking && !NetworkServer.active)
        {
            ClientCheckAndLoadMods();
            clientModsNeedRechecking = false;
        }
    }

    private void ServerInitMods()
    {
        addedMods.AddRange(ModManager.loadedMods);
    }

    private void ClientOnAddedModsCallback(SyncList<RingslingersMod>.Operation op, int itemIndex, RingslingersMod oldItem, RingslingersMod newItem)
    {
        clientModsNeedRechecking = true;
    }

    private void ClientCheckAndLoadMods()
    {
        try
        {
            ModManager.TrySyncMods(addedMods.ToArray(), (wasSuccessful, message) => ClientOnModLoaded(wasSuccessful, message));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void ClientOnModLoaded(bool wasSuccessful, string message)
    {
        string errorMessage = null;

        if (!wasSuccessful)
            errorMessage = $"Errors loading mods:\n{message}";
        else
            Debug.Log($"Successfully loaded mods from server");

        if (errorMessage != null)
            Netplay.singleton.DisconnectSelfWithMessage($"The server added a mod that triggered an error:\n\n{errorMessage}", true);
    }

    private void OnModLoaded(RingslingersMod mod)
    {
        if (NetworkServer.active)
            addedMods.Add(mod);
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
