using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TestBotBehaviours))]
public class TestBotBehaviourEditor : Editor
{
    private void OnSceneGUI()
    {
        TestBotBehaviours tester = (TestBotBehaviours)target;
        EditorGUI.BeginChangeCheck();

        Vector3 newTargetPosition = Handles.PositionHandle(tester.targetPosition, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "test bot thing");
            tester.targetPosition = newTargetPosition;
        }
    }
}
