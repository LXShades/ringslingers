using Mirror;
using System;
using UnityEngine.SceneManagement;

public class Player : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [NonSerialized] [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId = -1;

    /// <summary>
    /// Whether this client is an admin
    /// </summary>
    [NonSerialized] [SyncVar] public bool isAdmin;

    private Character character => playerId != -1 && playerId < Netplay.singleton.players.Count ? Netplay.singleton.players[playerId] : null;

    /// <summary>
    /// Persistent data for the local player
    /// </summary>
    public static PlayerInfo localPersistentPlayerInfo
    {
        get => _localPersistent;
        set
        {
            _localPersistent = value;

            if (Netplay.singleton && Netplay.singleton.localClient != null)
                Netplay.singleton.localClient.CmdSendPersistentData(value);
        }
    }
    private static PlayerInfo _localPersistent;

    private PlayerInfo playerInfo;

    // [server] predicted servertime of the last input we received from the client, recorded here because history is trimmed but we need to inform the client of how far ahead it was
    public double serverTimeOfLastReceivedInput { get; set; }

    public override void OnStartServer()
    {
        // Players are now persistent between maps
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += ServerOnSceneChanged;

        base.OnStartServer();
    }

    public override void OnStartLocalPlayer()
    {
        // Players are now persistent between maps
        DontDestroyOnLoad(gameObject);

        base.OnStartLocalPlayer();

        Netplay.singleton.localPlayerId = playerId;

        // Request our name upon joining
        CmdSendPersistentData(localPersistentPlayerInfo);
        CmdRequestCharacterSpawn();
    }

    public void OnStartBot()
    {
        ServerSetupCharacter();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= ServerOnSceneChanged;
    }

    private void ServerOnSceneChanged(Scene scene, LoadSceneMode mode)
    {
        ServerSetupCharacter();
    }

    void OnPlayerIdChanged(int oldValue, int newValue)
    {
        playerId = newValue;

        if (isLocalPlayer)
        {
            Netplay.singleton.localPlayerId = newValue;
        }
    }

    /// <summary>
    /// Spawns and sets up this player's character if necessary
    /// </summary>
    private void ServerSetupCharacter()
    {
        // Change existing character
        if (playerInfo.characterIndex >= 0 && playerInfo.characterIndex < RingslingersContent.loaded.characters.Count && character != null && character.characterIndex != playerInfo.characterIndex)
        {
            Character player = Netplay.singleton.ChangePlayerCharacter(playerId, playerInfo.characterIndex, this);

            if (player != null)
            {
                playerId = player.playerId;
                player.netIdentity.AssignClientAuthority(netIdentity.connectionToClient);
            }
        }

        // Spawn new character, if needed
        if (character == null)
        {
            Character newPlayer = Netplay.singleton.AddCharacter(playerInfo.characterIndex, this);

            if (netIdentity.connectionToClient != null)
                newPlayer.netIdentity.AssignClientAuthority(netIdentity.connectionToClient);

            playerId = newPlayer.playerId;
        }

        // Name
        string oldName = character.playerName;

        if (oldName != playerInfo.name)
        {
            MessageFeed.Post($"{oldName} was renamed to <player>{character.playerName}</player>");
            character?.Rename(playerInfo.name);
        }

        // Colour
        character.TryChangeColour(playerInfo.colour);
    }

    [Command]
    public void CmdSendPersistentData(PlayerInfo persistentData)
    {
        playerInfo = persistentData;
    }

    /// <summary>
    /// Sent when the player is ready / new scene loaded
    /// </summary>
    [Command]
    private void CmdRequestCharacterSpawn()
    {
        ServerSetupCharacter();

        // local player character created, update everyone else's outline statuc
        foreach (Character character in Netplay.singleton.players)
        {
            if (character)
                character.UpdateOutlineColour();
        }
    }

    // says something to the world
    [Command]
    public void CmdSendMessage(string message)
    {
        message = message.Replace("</noparse>", "lol"); // plz don't
        MessageFeed.Post($"<{character?.playerName}> <noparse>{message}</noparse>", true);
    }

    [TargetRpc]
    public void TargetPostMessage(string message, bool doBeep)
    {
        MessageFeed.PostLocal(message, doBeep);
    }
}
