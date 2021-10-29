using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JsonThingImporter))]
public class JsonThingImporterEditor : Editor
{
	public override void OnInspectorGUI()
    {
		base.OnInspectorGUI();

		if (GUILayout.Button("Load Json..."))
        {
            string filename = EditorUtility.OpenFilePanel("Read Json file", Application.dataPath, "json");

            if (!string.IsNullOrEmpty(filename))
            {
                JsonThingImporter.ExtractedThings things = JsonUtility.FromJson<JsonThingImporter.ExtractedThings>(System.IO.File.ReadAllText(filename));

                if (things != null)
                {
                    Transform transform = (target as JsonThingImporter).transform;
                    for (int i = transform.childCount - 1; i >= 0; i--)
                        DestroyImmediate(transform.GetChild(i).gameObject);

                    (target as JsonThingImporter).ReadThings(things.things);
                }
                else
                {
                    Debug.LogError($"Couldn't read {filename}, invalid format?");
                }
            }
        }
    }
}
