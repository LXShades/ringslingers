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
        for (int i = 0; i < addedMods.Count; i++)
        {
            if (i < ModManager.loadedMods.Count)
            {
                if (addedMods[i].filename != ModManager.loadedMods[i].filename)
                    Netplay.singleton.DisconnectSelfWithMessage($"Mod loaded at idx={i} '{ModManager.loadedMods[i].filename}' does not match server mod requested at idx={i} '{addedMods[i].filename}'.", true);
                else
                    continue;
            }
            else
            {
                ulong originalModHash = addedMods[i].hash;
                RingslingersMod modToLoad = addedMods[i];

                ModManager.LoadMods(new RingslingersMod[] { modToLoad }, (wasSuccessful, message) => ClientOnModLoaded(modToLoad, originalModHash, wasSuccessful, message ));
            }
        }
    }

    private void ClientOnModLoaded(RingslingersMod mod, ulong originalModHash, bool wasSuccessful, string message)
    {
        string errorMessage = null;

        if (!wasSuccessful)
            errorMessage = $"Mod '{mod.filename}': {message})";
        else if (ModManager.GetModHash(mod.filename) != originalModHash)
            errorMessage = $"Mod '{mod.filename}' has an incorrect hash, wrong version?";
        else
            Debug.Log($"Successfully loaded mod \"{mod.filename}\" from server");

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
