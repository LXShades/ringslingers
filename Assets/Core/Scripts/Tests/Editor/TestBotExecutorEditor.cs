using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(TestBotExecutor))]
public class TestBotExecutorEditor : Editor
{
    private double lastUpdateTime;

    private void OnEnable()
    {
        EditorApplication.update += OnUpdate;
        lastUpdateTime = Time.realtimeSinceStartupAsDouble;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnUpdate;
    }

    private void OnUpdate()
    {
        TestBotExecutor tester = (TestBotExecutor)target;
        if (tester.autoplay)
        {
            float deltaTime = (float)(Time.realtimeSinceStartupAsDouble - lastUpdateTime);
            tester.stateTime = (tester.stateTime + deltaTime) % tester.simulationDuration;

            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();

            lastUpdateTime = Time.realtimeSinceStartupAsDouble;
        }
    }

    private void OnSceneGUI()
    {
        TestBotExecutor tester = (TestBotExecutor)target;

        for (int index = 0; index < tester.targetPositions.Count; index++)
        {
            EditorGUI.BeginChangeCheck();

            Vector3 newTargetPosition = Handles.PositionHandle(tester.targetPositions[index], Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tester, "test bot thing");
                tester.targetPositions[index] = newTargetPosition;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.HelpBox("Change the type of action below. Note this will reset the action data.", MessageType.Info);
        foreach (var type in typeof(TestBotAction_RunToPoints).Assembly.GetTypes().Where(x => typeof(TestBotAction).IsAssignableFrom(x)))
        {
            if (EditorGUILayout.LinkButton(type.Name))
            {
                Undo.RecordObject(target, "change testbot action type");
                (target as TestBotExecutor).actionToPerform = (TestBotAction)type.GetConstructor(System.Array.Empty<System.Type>()).Invoke(null);
            }
        }
    }
}
