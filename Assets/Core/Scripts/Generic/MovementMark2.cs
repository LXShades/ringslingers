using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;

public class MovementMark2 : WorldObjectComponent
{
    [Serializable]
    public class MoveState
    {
        public float time;
        public Vector3 position;
        public Vector3 velocity;

        public MoveState() { }

        public MoveState(MovementMark2 source)
        {
            From(source);
        }

        public void From(MovementMark2 source)
        {
            time = World.live.localTime;
            position = source.transform.position;
            velocity = source.velocity;
        }

        public void To(MovementMark2 target)
        {
            target.transform.position = position;
            target.velocity = velocity;
        }
    }

    public Vector3 serverPosition;
    public Vector3 serverVelocity;
    public float serverLocalTime;

    public float lastMoveLocalTime;

    public History<PlayerInput> inputHistory = new History<PlayerInput>();

    public Vector3 velocity;

    private MoveState pendingReconcilationSnapshot = null;

    private bool inputWasSetThisFrame = false;

    public override void WorldUpdate(float deltaTime)
    {
        if (pendingReconcilationSnapshot != null)
        {
            OnReconcile(pendingReconcilationSnapshot);
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

        TickMovement(deltaTime, inputHistory.Count > 0 ? inputHistory[0] : new PlayerInput(), lastInput, false);
        inputWasSetThisFrame = false;
    }

    public virtual void TickMovement(float deltaTime, PlayerInput commands, PlayerInput lastCommands, bool isResimulated = false)
    {
    }

    /// <summary>
    /// Called when this object is summoned to a position by the server
    /// </summary>
    public virtual void OnReconcile(MoveState snapshot)
    {
        pendingReconcilationSnapshot.To(this);
        pendingReconcilationSnapshot = null;
    }

    public MoveState GetSnapshot()
    {
        return new MoveState(this);
    }

    public void SetInput(float localTime, PlayerInput input)
    {
        inputHistory.Insert(localTime, input);

        // todo set velocity from client
        serverPosition = transform.position;
        serverVelocity = velocity;
        serverLocalTime = localTime;
        inputWasSetThisFrame = true;
    }

    public void Reconcile(MoveState snapshot)
    {
        pendingReconcilationSnapshot = snapshot;
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