﻿using System;
using UnityEditor;
using UnityEngine;

public static class ObjectFlipper
{
    [MenuItem("Tools/Flip/Global X")]
    public static void FlipGlobalX()
    {
        ForSelectedNonChildObjects(go =>
        {
            Undo.RecordObject(go.transform, "Flip X");
            go.transform.position = new Vector3(-go.transform.position.x, go.transform.position.y, go.transform.position.z);
        });
    }

    [MenuItem("Tools/Flip/Global Z")]
    public static void FlipGlobalZ()
    {
        ForSelectedNonChildObjects(go =>
        {
            Undo.RecordObject(go.transform, "Flip Z");
            go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, -go.transform.position.z);
        });
    }

    private static void ForSelectedNonChildObjects(Action<GameObject> predicate)
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            for (Transform parent = go.transform.parent; parent != null; parent = parent.transform.parent)
            {
                if (Selection.Contains(parent.gameObject)) // only flip the highest level selections please
                    goto SkipObject;
            }

            predicate?.Invoke(go);
            SkipObject:;
        }
    }
}