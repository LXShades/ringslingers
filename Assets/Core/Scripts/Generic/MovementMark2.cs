using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MovementMark2 : WorldObjectComponent
{
    public float futureness = 0.3f;

    [Range(0.01f, 0.5f)]
    public float resimulationStep = 0.08f;

    [Serializable]
    public class MoveState
    {
        public PlayerInput input;
        public float time;
        public Vector3 position;
        public Vector3 velocity;
        public CharacterMovement.State state;

        public MoveState() { }

        public MoveState(MovementMark2 source)
        {
            Make(source);
        }

        public void Make(MovementMark2 source)
        {
            time = World.live.localTime;
            position = source.transform.position;
            velocity = source.velocity;
            input = source.moveHistory.Count > 0 ? source.moveHistory[0].input : new PlayerInput();
            state = (source as CharacterMovement).state;
        }

        public void Apply(MovementMark2 target)
        {
            target.transform.position = position;
            target.velocity = velocity;
            (target as CharacterMovement).state = state;
        }
    }

    public float lastMoveLocalTime;

    public History<MoveState> moveHistory = new History<MoveState>();

    private PlayerInput input;

    public Vector3 velocity;

    private MoveState pendingMoveState = null;
    private MoveState lastReceivedMoveState = null;

    public float movementAccuracyTolerance = 0.3f;

    private float lastExecutedInput;

    public int testReconcilationTime = 0;

    public override void WorldUpdate(float deltaTime)
    {
        // Tick movement locally here
        TickMovement(deltaTime, input, moveHistory.Count > 0 ? moveHistory[0].input : new PlayerInput(), false);
        moveHistory.Insert(World.live.localTime, new MoveState(this)
        {
            input = input
        });
        moveHistory.Prune(moveHistory.TimeAt(0) - 1);

        if (moveHistory.Count > 1)
        {
            Debug.Log($"Ticked {moveHistory[1].time}->{moveHistory[0].time} (+{moveHistory[1].time - moveHistory[0].time})");
        }

        // Receive latest networked input
        if (pendingMoveState != null)
        {
            if (NetworkServer.active && !hasAuthority)
            {
                // validate this player's movement on the server
                ServerValidateMovement(pendingMoveState);
            }
            else if (!NetworkServer.active)
            {
                // validate this player (or our own) movement on the client
                ClientValidateMovement(pendingMoveState);
            }

            pendingMoveState = null;
        }

        // Send the movement state occasionally AFTER movement has executed
        // (this should treat jumps, etc a bit nicer)
        if ((int)((World.live.localTime - deltaTime) * Netplay.singleton.playerTickrate) != (int)(World.live.localTime * Netplay.singleton.playerTickrate))
        {
            SendMovementState();
        }

        if (testReconcilationTime > -1)
        {
            TestReconcile();
        }
    }

    private void TestReconcile()
    {
        int startState = testReconcilationTime + 1;

        if (moveHistory.Count <= startState)
        {
            return;
        }

        MoveState moveState = moveHistory[startState];

        // hop back to server position
        moveState.Apply(this);

        // each item in movement history contains
        // 1. input
        // 2. state/movement AFTER input was executed
        // so if we were to hop back to moveState[1], we would tick from moveState[0] onwards

        Debug.Log($"Set position to {startState}");

        // resimulate to our own position
        for (int i = moveHistory.IndexAt(moveState.time); i >= 1; i--)
        {
            int input = i - 1;
            int lastInput = i;

            Debug.Log($"Simmed input {input} last {lastInput} {moveHistory[i].time}->{moveHistory[i - 1].time} (+{moveHistory[i - 1].time - moveHistory[i].time})");
            TickMovement(moveHistory[i - 1].time - moveHistory[i].time, moveHistory[input].input, moveHistory[lastInput].input, true);
        }
    }

    public virtual void TickMovement(float deltaTime, PlayerInput commands, PlayerInput lastCommands, bool isResimulated = false) { }

    public void SetInput(float localTime, PlayerInput input)
    {
        this.input = input;
    }

    private void SendMovementState()
    {
        // send movement to server
        if (netIdentity.hasAuthority)
        {
            CmdSendMovement(new MoveState(this));
        }

        if (NetworkServer.active)
        {
            RpcSendMovement(new MoveState(this)
            {
                time = lastReceivedMoveState != null ? lastReceivedMoveState.time : 0
            });
        }
    }

    [Command(channel = Channels.DefaultUnreliable)]
    public void CmdSendMovement(MoveState moveState)
    {
        OnReceivedMovement(moveState);
    }

    [ClientRpc(channel = Channels.DefaultUnreliable)]
    public void RpcSendMovement(MoveState moveState)
    {
        if (NetworkServer.active)
        {
            return;
        }

        OnReceivedMovement(moveState);
    }

    private void OnReceivedMovement(MoveState moveState)
    {
        if (lastReceivedMoveState == null || moveState.time > lastReceivedMoveState.time) // don't receive out-of-order movements
        {
            lastReceivedMoveState = moveState;
            pendingMoveState = moveState;
        }
    }

    private void ServerValidateMovement(MoveState moveState)
    {
        if (Vector3.Distance(moveState.position, transform.position) < movementAccuracyTolerance && false)
        {
            moveState.Apply(this);
        }

        SetInput(moveState.time, moveState.input);
    }

    private void ClientValidateMovement(MoveState moveState)
    {
        if (!hasAuthority)
        {
            // this is another player, just plop them here
            moveState.Apply(this);
            SetInput(moveState.time, moveState.input);

            for (float i = 0; i < futureness; i += resimulationStep)
            {
                TickMovement(Mathf.Min(resimulationStep, futureness - i), moveState.input, moveState.input, true);
            }
        }
        else
        {
            Debug.Log($"Got position ping: {World.live.localTime - moveState.time}");

            // hop back to server position
            moveState.Apply(this);

            // resimulate to our own position
            for (int i = moveHistory.IndexAt(moveState.time); i >= 1; i--)
            {
                TickMovement(moveHistory[i - 1].time - moveHistory[i].time, moveHistory[i - 1].input, moveHistory[i].input, true);
            }
        }
    }
}

[Serializable]
public class History<T>
{
    [Serializable]
    public struct HistoryItem
    {
        public float time;
        public T item;
    }

    [SerializeField] private List<HistoryItem> items = new List<HistoryItem>();

    public T this[int index]
    {
        get => items[index].item;
        set => items[index] = new HistoryItem()
        {
            time = items[index].time,
            item = value
        };
    }

    public int Count => items.Count;

    public T ItemAt(float time)
    {
        return items.Find(a => a.time == time).item;
    }

    public int IndexAt(float time)
    {
        return items.FindIndex(a => a.time == time);
    }

    public float TimeAt(int index)
    {
        return items[index].time;
    }

    public void Insert(float time, T item)
    {
        int index = 0;
        for (index = 0; index < items.Count; index++)
        {
            if (time >= items[index].time)
                break;
        }

        items.Insert(index, new HistoryItem() { item = item, time = time });
    }

    public void Clear()
    {
        items.Clear();
    }

    public void RemoveAt(int index)
    {
        items.RemoveAt(index);
    }

    public void Prune(float minTime)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].time < minTime)
            {
                items.RemoveRange(i, items.Count - i);
            }
        }
    }
}