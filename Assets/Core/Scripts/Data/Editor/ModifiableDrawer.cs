using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(IModifiableBase), true)]
public class ModifiableDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string labelPrefix = null;

        switch ((DataModFunction)property.FindPropertyRelative(nameof(DataMod<bool>.function)).intValue)
        {
            case DataModFunction.Add: labelPrefix = "* [+] "; break;
            case DataModFunction.Multiply: labelPrefix = "* [*] "; break;
            case DataModFunction.Override: labelPrefix = "* [!] "; break;
            default: break;
        }

        const float valueWidth = 0.6f;
        const float labelWidth = 0.2f;
        const float functionWidth = 0.2f;
        if (labelPrefix != null)
        {
            GUI.color = Color.yellow;
            EditorGUI.LabelField(new Rect(position.xMin, position.yMin, position.width * labelWidth, position.height), labelPrefix + property.displayName);
        }
        else
        {
            EditorGUI.LabelField(new Rect(position.xMin, position.yMin, position.width * labelWidth, position.height), property.displayName);
        }

        SerializedProperty valueProperty = property.FindPropertyRelative(nameof(DataMod<bool>.value));

        EditorGUI.PropertyField(new Rect(position.xMin + position.width * labelWidth, position.yMin, position.width * valueWidth, EditorGUI.GetPropertyHeight(valueProperty)), valueProperty, new GUIContent(""), true);
        EditorGUI.PropertyField(new Rect(position.xMin + position.width * (labelWidth + valueWidth), position.yMin, position.width * functionWidth, position.height), property.FindPropertyRelative(nameof(DataMod<bool>.function)), new GUIContent(""), false);

        property.serializedObject.ApplyModifiedProperties();

        GUI.color = Color.white;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(DataMod<bool>.value)));
    }
}
