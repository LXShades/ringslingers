using System;
using UnityEditor;
using UnityEngine;

public static class ObjectFlipper
{
    [MenuItem("Tools/Transform Selected Objects/Position: Flip Global X")]
    public static void FlipGlobalX()
    {
        ForSelectedNonChildObjects(go =>
        {
            Undo.RecordObject(go.transform, "Flip X");
            go.transform.position = new Vector3(-go.transform.position.x, go.transform.position.y, go.transform.position.z);
        });
    }

    [MenuItem("Tools/Transform Selected Objects/Position: Flip Global Z")]
    public static void FlipGlobalZ()
    {
        ForSelectedNonChildObjects(go =>
        {
            Undo.RecordObject(go.transform, "Flip Z");
            go.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, -go.transform.position.z);
        });
    }

    [MenuItem("Tools/Transform Selected Objects/Position+Rotation: Global Rotate 180 Y")]
    public static void GlobalRotate180Y()
    {
        Quaternion flipRotation = Quaternion.AngleAxis(180f, Vector3.up);
        ForSelectedNonChildObjects(go =>
        {
            Undo.RecordObject(go.transform, "Rotate 180");
            go.transform.position = new Vector3(-go.transform.position.x, go.transform.position.y, -go.transform.position.z);
            go.transform.rotation = flipRotation * go.transform.rotation;
        });
    }


    [MenuItem("Tools/Transform Selected Objects/Position+Rotation: Pin to nearby surface")]
    public static void PinToGround()
    {
        Quaternion flipRotation = Quaternion.AngleAxis(180f, Vector3.up);
        const float maxDistance = 5f;

        ForSelectedNonChildObjects(go =>
        {
            Undo.RecordObject(go.transform, "Pin to nearby sphere and slopes");

            // figure out which way to pin
            Vector3 directionToPin = -go.transform.up;

            PhysicsExtensions.Parameters parameters = new PhysicsExtensions.Parameters() { ignoreObject = go };

            if (PhysicsExtensions.Raycast(go.transform.position - directionToPin * 1f, directionToPin, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Ignore, in parameters))
            {
                // point up
                go.transform.rotation = Quaternion.FromToRotation(go.transform.up, hit.normal) * go.transform.rotation;

                // raycast again towards the target this time
                if (PhysicsExtensions.Raycast(go.transform.position, -hit.normal, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore, in parameters))
                {
                    go.transform.position += -hit.normal * hit.distance;
                }
            }
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
