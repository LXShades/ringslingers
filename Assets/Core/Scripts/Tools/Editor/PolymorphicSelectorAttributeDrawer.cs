using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PolymorphicTypeSelectorAttribute))]
public class PolymorphicSelectorAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string currentTypename = property.managedReferenceFullTypename;
        bool hasTypeMismatch = false;

        for (int i = 0; i < property.serializedObject.targetObjects.Length; i++)
        {
            if (i == 0)
                currentTypename = property.managedReferenceValue != null ? property.managedReferenceFullTypename : null;
            else
                hasTypeMismatch |= currentTypename != (property.managedReferenceValue != null ? property.managedReferenceFullTypename : null);
        }

        if (EditorGUI.DropdownButton(new Rect(position.x, position.y, position.width, 16), new GUIContent((hasTypeMismatch || currentTypename == null) ? "Select Type..." : currentTypename), FocusType.Keyboard))
        {
            GenericMenu menu = new GenericMenu();

            int spaceIdx = property.managedReferenceFieldTypename.IndexOf(' ');
            string assemblyName = property.managedReferenceFieldTypename.Substring(0, spaceIdx);
            string typeName = property.managedReferenceFieldTypename.Substring(spaceIdx + 1);
            System.Type fieldType = System.Type.GetType($"{typeName}, {assemblyName}");

            foreach (System.Type type in fieldType.Assembly.GetTypes().Where(x => fieldType.IsAssignableFrom(x)))
            {
                System.Type captureType = type;
                menu.AddItem(new GUIContent(captureType.Name), false, () =>
                {
                    foreach (Object target in property.serializedObject.targetObjects)
                    {
                        property.managedReferenceValue = captureType.GetConstructor(System.Array.Empty<System.Type>()).Invoke(null);
                        property.serializedObject.ApplyModifiedProperties();
                    }
                });
            }
            menu.ShowAsContext();
        }

        EditorGUI.PropertyField(new Rect(position.x, position.y + 16, position.width, position.height), property, label, true);
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property) + 16;
    }
}