using Mirror;

public class Player : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId;

    private Mirror.HistoryList<int> testList = new Mirror.HistoryList<int>();

    private Character character => playerId != -1 ? Netplay.singleton.players[playerId] : null;

    public static LocalPersistentPlayer localPersistent
    {
        get => _localPersistent;
        set
        {
            _localPersistent = value;

            if (Netplay.singleton.localClient != null)
            {
                Netplay.singleton.localClient.CmdSendPersistentData(value);
            }
        }
    }
    private static LocalPersistentPlayer _localPersistent;

    public override void OnStartServer()
    {
        NetworkConnection connection = netIdentity.connectionToClient;

        // spawn the player
        Character newPlayer = Netplay.singleton.AddPlayer(0);
        newPlayer.netIdentity.AssignClientAuthority(connection);

        playerId = newPlayer.playerId;

        base.OnStartServer();
    }

    public override void OnStartLocalPlayer()
    {
        // Request our name upon joining
        base.OnStartLocalPlayer();

        Netplay.singleton.localPlayerId = playerId;
        CmdSendPersistentData(localPersistent);
    }

    void OnPlayerIdChanged(int oldValue, int newValue)
    {
        playerId = newValue;

        if (isLocalPlayer)
        {
            Netplay.singleton.localPlayerId = newValue;
        }
    }

    [Command]
    public void CmdSendPersistentData(LocalPersistentPlayer persistentData)
    {
        // Name
        string oldName = character.playerName;

        character?.Rename(persistentData.name);

        if (oldName != character.playerName)
            MessageFeed.Post($"{oldName} was renamed to <player>{character.playerName}</player>");

        // Character
        if (persistentData.characterIndex >= 0 && persistentData.characterIndex < GameManager.singleton.playerCharacters.Length && character != null && character.characterIndex != persistentData.characterIndex)
        {
            Character player = Netplay.singleton.ChangePlayerCharacter(playerId, persistentData.characterIndex);

            if (player != null)
            {
                playerId = player.playerId;
                player.netIdentity.AssignClientAuthority(netIdentity.connectionToClient);
            }
        }

        // Colour
        if (character != null)
            character.TryChangeColour(persistentData.colour);
    }

    [Command]
    public void CmdSendMessage(string message)
    {
        message = message.Replace("</noparse>", "lol"); // plz don't
        MessageFeed.Post($"<{character?.playerName}> <noparse>{message}</noparse>", true);
    }
}
