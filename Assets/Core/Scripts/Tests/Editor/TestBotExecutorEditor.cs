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
            tester.watchTime = (tester.watchTime + deltaTime) % tester.simulationDuration;

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
}
