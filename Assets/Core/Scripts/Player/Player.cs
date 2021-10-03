using Mirror;

public class Player : NetworkBehaviour
{
    /// <summary>
    /// ID of this client
    /// </summary>
    [SyncVar(hook = nameof(OnPlayerIdChanged))] public int playerId;

    private TimelineList<int> testList = new TimelineList<int>();

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

    public float lastInputEarlyness { get; set; }

    public override void OnStartServer()
    {
        // spawn the player
        Character newPlayer = Netplay.singleton.AddPlayer(0);
        
        if (netIdentity.connectionToClient != null)
            newPlayer.netIdentity.AssignClientAuthority(netIdentity.connectionToClient);

        playerId = newPlayer.playerId;

        base.OnStartServer();
    }

    public override void OnStartLocalPlayer()
    {
        // Request our name upon joining
        base.OnStartLocalPlayer();

        Netplay.singleton.localPlayerId = playerId;

        // local player character created, update everyone else's outline statuc
        foreach (Character character in Netplay.singleton.players)
            character.UpdateOutlineColour();

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
