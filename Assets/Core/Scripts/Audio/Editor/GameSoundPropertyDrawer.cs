using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GameSound))]
public class GameSoundPropertyDrawer : PropertyDrawer
{
    private static GameSound copiedSound;
    private const int _kCopyPasteWidth = 60;

    GameSound[] presetValues = new GameSound[]
    {
        new GameSound() {minRange = 5, midRange = 22.5f, maxRange = 50f },
        new GameSound() {minRange = 5, midRange = 10.5f, maxRange = 50f }
    };

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        System.Type targetType = prop.serializedObject.targetObject.GetType();
        System.Reflection.FieldInfo field = targetType.GetField(prop.name);

        SerializedProperty originalProp = prop.Copy();

        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(pos, prop, label, true);

        if (GUI.Button(new Rect(pos.x + pos.width - _kCopyPasteWidth * 2, pos.yMax - 24, _kCopyPasteWidth, 24), "Copy"))
        {
            copiedSound = (field.GetValue(prop.serializedObject.targetObject) as GameSound).Clone();
        }

        if (GUI.Button(new Rect(pos.x + pos.width - _kCopyPasteWidth, pos.yMax - 24, _kCopyPasteWidth, 24), "Paste"))
        {
            if (copiedSound != null)
            {
                Undo.RecordObject(prop.serializedObject.targetObject, "Paste sound");
                field.SetValue(prop.serializedObject.targetObject, copiedSound);
                prop.serializedObject.Update();
                EditorUtility.SetDirty(prop.serializedObject.targetObject);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            const string rangePreset = nameof(GameSound.rangePreset);
            if (!SerializedProperty.DataEquals(prop.FindPropertyRelative(rangePreset), originalProp.FindPropertyRelative(rangePreset)))
            {
                int newPresetIndex = prop.FindPropertyRelative(rangePreset).intValue - 1;

                if (newPresetIndex >= 0)
                {
                    // set range values from new preset
                    prop.FindPropertyRelative(nameof(GameSound.minRange)).floatValue = presetValues[newPresetIndex].minRange;
                    prop.FindPropertyRelative(nameof(GameSound.midRange)).floatValue = presetValues[newPresetIndex].midRange;
                    prop.FindPropertyRelative(nameof(GameSound.maxRange)).floatValue = presetValues[newPresetIndex].maxRange;
                }
            }
            else
            {
                // set preset to custom if range values have changed
                if (!SerializedProperty.DataEquals(prop.FindPropertyRelative(nameof(GameSound.minRange)), originalProp.FindPropertyRelative(nameof(GameSound.minRange))) 
                    || !SerializedProperty.DataEquals(prop.FindPropertyRelative(nameof(GameSound.midRange)), originalProp.FindPropertyRelative(nameof(GameSound.midRange)))
                    || !SerializedProperty.DataEquals(prop.FindPropertyRelative(nameof(GameSound.maxRange)), originalProp.FindPropertyRelative(nameof(GameSound.maxRange))))
                {
                    prop.FindPropertyRelative(nameof(GameSound.rangePreset)).intValue = (int)GameSound.RangePreset.Custom;
                }
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label) + 24;
    }
}