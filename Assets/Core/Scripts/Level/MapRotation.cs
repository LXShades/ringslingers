using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class MapRotation
{
    public string name = "Some Random Maps";
    [FormerlySerializedAs("levels")]
    public List<MapConfiguration> maps = new List<MapConfiguration>();
}
