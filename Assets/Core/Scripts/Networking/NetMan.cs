using Mirror;
using UnityEngine.SceneManagement;

public class NetMan : NetworkManager
{
    public static new NetMan singleton { get; private set; }

    public delegate void ConnectionEvent(NetworkConnection connection);
    public delegate void BasicEvent();

    /// <summary>
    /// Connected to the server as a client
    /// </summary>
    public ConnectionEvent onClientConnect;

    /// <summary>
    /// Disconnected from the server as a client
    /// </summary>
    public ConnectionEvent onClientDisconnect;

    /// <summary>
    /// Client has connected to my server
    /// </summary>
    public ConnectionEvent onServerConnect;

    /// <summary>
    /// Client has disconnected from my server
    /// </summary>
    public ConnectionEvent onServerDisconnect;

    /// <summary>
    /// Server has been started
    /// </summary>
    public BasicEvent onServerStarted;

    /// <summary>
    /// Player was added on the server
    /// </summary>
    public ConnectionEvent onServerAddPlayer;

    public override void Awake()
    {
        base.Awake();

        singleton = this;

        SyncActionChain.RegisterHandlers();
    }

    public void Host(bool withLocalPlayer)
    {
        if (withLocalPlayer)
            StartHost();
        else
            StartServer();

        networkSceneName = SceneManager.GetActiveScene().name;
    }

    public void Connect(string ip)
    {
        networkAddress = ip;
        StartClient();
    }

    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);
        onClientConnect?.Invoke(conn);
    }

    public override void OnClientDisconnect(NetworkConnection conn)
    {
        base.OnClientDisconnect(conn);
        onClientDisconnect?.Invoke(conn);

        StopClient();
    }

    public override void OnServerConnect(NetworkConnection conn)
    {
        base.OnServerConnect(conn);
        onServerConnect?.Invoke(conn);
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        onServerDisconnect?.Invoke(conn);
        base.OnServerDisconnect(conn);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        onServerStarted?.Invoke();
    }

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        base.OnServerAddPlayer(conn);
        onServerAddPlayer?.Invoke(conn);
    }
}
