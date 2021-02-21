using Mirror;
using System;
using System.Collections.Generic;

public class PlayerTicker : NetworkBehaviour
{
    private struct ServerPlayerTickMessage
    {
        public float serverTime;
        public ArraySegment<ServerPlayerTick> ticks;
    }

    private struct ServerPlayerTick
    {
        public byte id;
        public PlayerController.MoveStateWithInput moveState;
    }

    public static PlayerTicker singleton { get; private set; }

    private readonly List<ServerPlayerTick> ticksOut = new List<ServerPlayerTick>(32);

    private readonly Dictionary<int, NetFlowController<PlayerController.InputPack>> playerInputFlow = new Dictionary<int, NetFlowController<PlayerController.InputPack>>();

    private readonly NetFlowController<ServerPlayerTickMessage> serverTickFlow = new NetFlowController<ServerPlayerTickMessage>();

    // on clients, what server time are they aiming to predict
    // on server, local server time
    public float predictedServerTime { get; private set; }

    public float localPlayerPing { get; private set; }

    private void Awake()
    {
        singleton = this;
    }

    private void Update()
    {
        if (NetworkServer.active)
            predictedServerTime = UnityEngine.Time.realtimeSinceStartup; // we technically don't run a prediction of the server time on the server
        else
            predictedServerTime += UnityEngine.Time.unscaledDeltaTime; // we just tick it up and update properly occasionally

        // Client receive server ticks
        if (serverTickFlow.TryPopMessage(out ServerPlayerTickMessage serverTick, true))
            ApplyServerTick(serverTick);

        // Server receive inputs
        for (int i = 0; i < Netplay.singleton.players.Count; i++)
        {
            if (Netplay.singleton.players[i] && playerInputFlow.ContainsKey(i))
            {
                if (playerInputFlow[i].TryPopMessage(out PlayerController.InputPack inputPack, true))
                    Netplay.singleton.players[i].GetComponent<PlayerController>().ReceiveInputPack(inputPack);
            }
        }

        // All tick players
        foreach (Player player in Netplay.singleton.players)
        {
            if (player)
                player.GetComponent<PlayerController>().Tick();
        }

        // Client/server send messages
        if (Netplay.singleton.isPlayerTick)
        {
            if (NetworkServer.active)
            {
                ServerPlayerTickMessage tick = MakeTickMessage();

                RpcPlayerTick(tick);
            }
            else if (NetworkClient.isConnected)
            {
                if (Netplay.singleton.localPlayer)
                {
                    PlayerController.InputPack inputPack = Netplay.singleton.localPlayer.GetComponent<PlayerController>().MakeInputPack();

                    CmdPlayerInput(inputPack);
                }
            }
        }
    }

    private ServerPlayerTickMessage MakeTickMessage()
    {
        ticksOut.Clear();

        for (int i = 0; i < Netplay.singleton.players.Count; i++)
        {
            if (Netplay.singleton.players[i])
            {
                PlayerController controller = Netplay.singleton.players[i].GetComponent<PlayerController>();

                ticksOut.Add(new ServerPlayerTick()
                {
                    id = (byte)i,
                    moveState = new PlayerController.MoveStateWithInput()
                    {
                        moveState = controller.MakeOrGetConfirmedMoveState(),
                        input = controller.inputHistory.Latest.input
                    }
                });
            }
        }

        ServerPlayerTickMessage tick = new ServerPlayerTickMessage()
        {
            serverTime = UnityEngine.Time.realtimeSinceStartup,
            ticks = new ArraySegment<ServerPlayerTick>(ticksOut.ToArray()),
        };

        return tick;
    }

    private void ApplyServerTick(ServerPlayerTickMessage tickMessage)
    {
        foreach (var tick in tickMessage.ticks)
        {
            if (Netplay.singleton.players[tick.id])
            {
                PlayerController controller = Netplay.singleton.players[tick.id].GetComponent<PlayerController>();
                controller.ReceiveMovement(tick.moveState.moveState, tick.moveState.input);

                if (tick.id == Netplay.singleton.localPlayerId)
                    localPlayerPing = controller.clientPlaybackTime + controller.currentExtrapolation - (tick.moveState.moveState.time + tick.moveState.moveState.extrapolation);
            }
        }

        predictedServerTime = tickMessage.serverTime + localPlayerPing;
    }

    [ClientRpc(channel = Channels.DefaultUnreliable)]
    private void RpcPlayerTick(ServerPlayerTickMessage tickMessage)
    {
        if (!NetworkServer.active) // server should not receive server player ticks dur
            serverTickFlow.PushMessage(tickMessage, tickMessage.serverTime);
    }

    [Command(channel = Channels.DefaultUnreliable, ignoreAuthority = true)]
    private void CmdPlayerInput(PlayerController.InputPack inputPack, NetworkConnectionToClient source = null)
    {
        if (source.identity && source.identity.TryGetComponent(out PlayerClient client))
        {
            if (!playerInputFlow.ContainsKey(client.playerId))
                playerInputFlow.Add(client.playerId, new NetFlowController<PlayerController.InputPack>());

            float endTime = inputPack.startTime;
            foreach (var input in inputPack.inputs)
                endTime += input.deltaTime;

            playerInputFlow[client.playerId].PushMessage(inputPack, endTime);
        }
        else
        {
            Log.WriteWarning($"Cannot receive message from {source}: no player found");
        }
    }

    public float GetPlayerDelay(int playerId)
    {
        if (playerInputFlow.TryGetValue(playerId, out NetFlowController<PlayerController.InputPack> flow))
            return flow.currentDelay;
        else
            return 0f;
    }

    public string DebugInfo()
    {
        string playerInputFlowDebug = "";
        
        foreach (var pair in playerInputFlow)
        {
            if (Netplay.singleton.players[pair.Key])
            {
                playerInputFlowDebug += $"{Netplay.singleton.players[pair.Key].playerName} delay: {pair.Value}\n";
            }
        }

        return $"ServerTickFlow: {serverTickFlow}\nPing: {(int)(localPlayerPing * 1000)}ms\n{playerInputFlowDebug}";
    }
}
