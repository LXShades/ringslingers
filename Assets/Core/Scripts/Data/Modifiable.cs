using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ModifiableThing", menuName = "Modifiable")]
public class Modifiable : ScriptableObject
{
    public DataMod<float> speed;
    public DataMod<GameObject> ringPrefab;
    public DataMod<float[]> spawnables;
}

public enum DataModFunction
{
    /// <summary>
    /// Ignore if this is being applied to another value and retain the previous value.
    /// If this is a base, this is the base's value.
    /// </summary>
    Default,

    /// <summary>
    /// Override the previous value
    /// </summary>
    Override,

    /// <summary>
    /// Add this to the previous value
    /// </summary>
    Add,
    
    /// <summary>
    /// Multiply the previous value by this
    /// </summary>
    Multiply
}

public interface IModifiableBase { }

[System.Serializable]
public struct DataMod<T> : IModifiableBase
{
    public T value;
    public DataModFunction function;

    public T Apply(T input, bool isInputBase)
    {
        switch (function)
        {
            case DataModFunction.Default: return isInputBase ? value : input;
            case DataModFunction.Override: return input;
            case DataModFunction.Add: return (dynamic)value + (dynamic)input;
            case DataModFunction.Multiply: return (dynamic)value * (dynamic)input;
            default: return value; // shouldnt happen
        }
    }
}