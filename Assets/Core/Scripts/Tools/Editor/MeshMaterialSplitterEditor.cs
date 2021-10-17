using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshMaterialSplitter))]
public class MeshMaterialSplitterEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		if (GUILayout.Button("Convert"))
		{
			(target as MeshMaterialSplitter).PerformConversion();
		}
	}
}
