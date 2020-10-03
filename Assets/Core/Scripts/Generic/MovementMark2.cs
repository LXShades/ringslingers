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
    public class MoveState : IEquatable<MoveState>
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
            state = (source as CharacterMovement).state;
        }

        public void Apply(MovementMark2 target)
        {
            target.transform.position = position;
            target.velocity = velocity;
            (target as CharacterMovement).state = state;
        }

        public bool Equals(MoveState other)
        {
            return other.input.Equals(input) && other.position == position && other.velocity == velocity && other.state == state;
        }
    }

    public float lastMoveLocalTime;

    public History<MoveState> moveHistory = new History<MoveState>();

    private PlayerInput input;

    public Vector3 velocity;

    private MoveState pendingMoveState = null;
    private MoveState lastReceivedMoveState = null;

    private float lastExecutedInput;

    [Header("Reconcilation")]
    public int testReconcilationTime = 0;

    public bool alwaysReconcile = false;

    public float reconcilationPositionTolerance = 0.3f;

    public override void WorldUpdate(float deltaTime)
    {
        if (pendingMoveState != null && !hasAuthority)
        {
            input = pendingMoveState.input;
        }

        // Tick movement locally here
        TickMovement(deltaTime, input, moveHistory.Count > 0 ? moveHistory[0].input : new PlayerInput(), false);
        moveHistory.Insert(World.live.localTime, new MoveState(this)
        {
            input = input
        });
        moveHistory.Prune(moveHistory.TimeAt(0) - 1);

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

        if (testReconcilationTime > -1 && moveHistory.Count > testReconcilationTime)
        {
            TryReconcile(moveHistory[testReconcilationTime]);
        }
    }

    private void TryReconcile(MoveState pastState)
    {
        // hop back to server position
        pastState.Apply(this);

        // fast forward if we can
        int startState = moveHistory.IndexAt(pastState.time);

        if (startState > -1)
        {
            // each item in movement history contains
            // 1. input
            // 2. state/movement AFTER input was executed
            // so if we were to hop back to moveState[1], we tick by moveState[0].time - moveState[1].time using inputs from moveState[0].

            // resimulate to our own position
            for (int i = startState; i >= 1; i--)
            {
                int input = i - 1;
                int lastInput = i;

                TickMovement(moveHistory[input].time - moveHistory[lastInput].time, moveHistory[input].input, moveHistory[lastInput].input, true);

                moveHistory[input] = new MoveState(this) {
                    time = moveHistory[input].time,
                    input = moveHistory[input].input
                };
            }
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
            CmdSendMovement(new MoveState(this)
            {
                input = moveHistory.Count > 0 ? moveHistory[0].input : new PlayerInput(),
                time = World.live.localTime
            });
        }

        if (NetworkServer.active)
        {
            RpcSendMovement(new MoveState(this)
            {
                input = moveHistory.Count > 0 ? moveHistory[0].input : new PlayerInput(),
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

        if (hasAuthority)
        {
            return; // reconcilation movement is different
        }
        OnReceivedMovement(moveState);
    }

    /// <summary>
    /// Sent when the server confirms a different position for the client (or AlwaysReconcile is on)
    /// </summary>
    [TargetRpc(channel = Channels.DefaultUnreliable)]
    public void TargetSendReconcilationMovement(MoveState moveState)
    {
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
        SetInput(moveState.time, moveState.input);

        if (Vector3.Distance(moveState.position, transform.position) < reconcilationPositionTolerance && !alwaysReconcile)
        {
            moveState.Apply(this);
        }
        else
        {
            TargetSendReconcilationMovement(new MoveState(this)
            {
                input = moveState.input,
                time = moveState.time
            });

            Log.Write($"Remote reconcile time: {moveState.time} dist: {Vector3.Distance(moveState.position, transform.position)}");
        }
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
            MoveState localState = moveHistory.ItemAt(moveState.time);

            // usually we only reconcile movement if the server didn't accept our version of things
            if (alwaysReconcile || localState == null || !localState.Equals(moveState))
            {
                if (localState != null)
                {
                    Log.Write($"Reconciling movement (time: {moveState.time} distance: {Vector3.Distance(moveState.position, localState.position)}");
                }

                TryReconcile(moveState);
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