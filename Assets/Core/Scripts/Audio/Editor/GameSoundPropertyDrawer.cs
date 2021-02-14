using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GameSound))]
public class GameSoundPropertyDrawer : PropertyDrawer
{
    private static GameSound copiedSound;
    private const int _kCopyPasteWidth = 60;

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.PropertyField(pos, prop, label, true);

        if (GUI.Button(new Rect(pos.x + pos.width - _kCopyPasteWidth * 2, pos.yMax - 24, _kCopyPasteWidth, 24), "Copy"))
        {
            System.Type targetType = prop.serializedObject.targetObject.GetType();
            System.Reflection.FieldInfo field = targetType.GetField(prop.name);

            copiedSound = (field.GetValue(prop.serializedObject.targetObject) as GameSound).Clone();
        }

        if (GUI.Button(new Rect(pos.x + pos.width - _kCopyPasteWidth, pos.yMax - 24, _kCopyPasteWidth, 24), "Paste"))
        {
            if (copiedSound != null)
            {
                System.Type targetType = prop.serializedObject.targetObject.GetType();
                System.Reflection.FieldInfo field = targetType.GetField(prop.name);

                Undo.RecordObject(prop.serializedObject.targetObject, "Paste sound");
                field.SetValue(prop.serializedObject.targetObject, copiedSound);
                prop.serializedObject.Update();
                EditorUtility.SetDirty(prop.serializedObject.targetObject);
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label) + 24;
    }
}