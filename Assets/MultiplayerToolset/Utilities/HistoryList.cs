using System;
using System.Collections.Generic;
using UnityEngine;

public class HistoryListBase
{
    public virtual int Count { get; }

    public virtual float LatestTime { get; }

    public virtual void Clear() { }

    public virtual int IndexAt(float time, float tolerance = 0.01f) { return -1; }
    public virtual float TimeAt(int index) { return -1f; }
    public virtual void RemoveAt(int index) { }
    public virtual void Prune(float minTime) { }
}

[Serializable]
public class HistoryList<T> : HistoryListBase
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

    public override int Count => items.Count;

    public T Latest => items.Count > 0 ? items[0].item : default;

    public override float LatestTime => items.Count > 0 ? items[0].time : 0.0f;

    public T ItemAt(float time, float tolerance = 0.01f)
    {
        return items.Find(a => a.time >= time - tolerance && a.time <= time + tolerance).item;
    }

    public override int IndexAt(float time, float tolerance = 0.01f)
    {
        return items.FindIndex(a => a.time >= time - tolerance && a.time <= time + tolerance);
    }

    public override float TimeAt(int index)
    {
        if (index == -1) return -1;

        return items[index].time;
    }

    public int ClosestIndexAfter(float time, float tolerance = 0.01f)
    {
        if (items.Count > 0)
        {
            for (int index = items.Count - 1; index >= 0; index--)
            {
                if (items[index].time > time - tolerance)
                    return index;
            }
        }

        return -1;
    }

    public int ClosestIndexBefore(float time, float tolerance = 0.01f)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].time < time + tolerance)
                return index;
        }

        return -1;
    }

    public int ClosestIndexBeforeOrEarliest(float time, float tolerance = 0.01f)
    {
        int index = ClosestIndexBefore(time, tolerance);
        
        if (index == -1)
        {
            return items.Count - 1;
        }
        else
        {
            return index;
        }
    }

    public void Set(float time, T item, float tolerance = 0.01f)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (time - tolerance <= items[index].time && time + tolerance >= items[index].time)
            {
                items[index] = new HistoryItem() { item = item, time = time };
                return;
            }
        }

        // none to replace, insert instead
        Insert(time, item);
    }

    public void Insert(float time, T item)
    {
        int index;
        for (index = 0; index < items.Count; index++)
        {
            if (time >= items[index].time)
                break;
        }

        items.Insert(index, new HistoryItem() { item = item, time = time });
    }

    public override void Clear()
    {
        items.Clear();
    }

    public override void RemoveAt(int index)
    {
        items.RemoveAt(index);
    }

    public override void Prune(float minTime)
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