using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModDefinition : ScriptableObject
{
    public RingslingersContent content;

    [Header("Build Date/Time"), HideInInspector]
    public string buildDate;
}
