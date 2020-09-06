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
            input = source.inputHistory.Count > 0 ? source.inputHistory[0] : new PlayerInput();
        }

        public void Apply(MovementMark2 target)
        {
            target.transform.position = position;
            target.velocity = velocity;
            target.SetInput(time, input);
        }
    }

    public float lastMoveLocalTime;

    public History<PlayerInput> inputHistory = new History<PlayerInput>();

    public Vector3 velocity;

    private MoveState pendingReconcilationSnapshot = null;
    private MoveState lastReceivedMoveState = null;

    private bool inputWasSetThisFrame = false;

    public override void WorldUpdate(float deltaTime)
    {
        if (pendingReconcilationSnapshot != null)
        {
            //OnReconcile(pendingReconcilationSnapshot);
        }

        PlayerInput lastInput = new PlayerInput();
        if (inputHistory.Count > 1)
        {
            if (inputWasSetThisFrame)
                lastInput = inputHistory[1];
            else
                lastInput = inputHistory[0];

            inputHistory.Prune(inputHistory.TimeAt(0) - 1);
        }

        if ((int)((World.live.localTime - deltaTime) * Netplay.singleton.playerTickrate) != (int)(World.live.localTime * Netplay.singleton.playerTickrate))
        {
            SendMovementState();
        }

        TickMovement(deltaTime, inputHistory.Count > 0 ? inputHistory[0] : new PlayerInput(), lastInput, false);
        inputWasSetThisFrame = false;
    }

    public virtual void TickMovement(float deltaTime, PlayerInput commands, PlayerInput lastCommands, bool isResimulated = false) { }

    public void SetInput(float localTime, PlayerInput input)
    {
        inputHistory.Insert(localTime, input);

        inputWasSetThisFrame = true;
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
        Debug.Log("Server received movement");
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
        if (!hasAuthority)
        {
            lastReceivedMoveState = moveState;
            moveState.Apply(this);

            for (float i = 0; i < futureness; i += resimulationStep)
            {
                TickMovement(Mathf.Min(resimulationStep, futureness - i), moveState.input, moveState.input, true);
            }
        }
        else
        {
            if (!NetworkServer.active)
            {
                Debug.Log($"Got position ping: {World.live.localTime - moveState.time}");
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

    private List<HistoryItem> items = new List<HistoryItem>();

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