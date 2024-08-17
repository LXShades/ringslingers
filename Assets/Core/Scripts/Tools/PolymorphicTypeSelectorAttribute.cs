using UnityEngine;

[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
public class PolymorphicTypeSelectorAttribute : PropertyAttribute
{
    public PolymorphicTypeSelectorAttribute()
    {
    }
}