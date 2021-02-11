using System.Collections.Generic;
using UnityEngine;

public class Path : MonoBehaviour
{
    public List<Vector3> points = new List<Vector3>();

    private List<float> distanceAtPoint = new List<float>();

    public float pathLength { get; private set; }

    private void Awake()
    {
        PrecalculatePath();
    }

    private void PrecalculatePath()
    {
        float length = 0;

        distanceAtPoint.Clear();

        for (int i = 0; i < points.Count; i++)
        {
            distanceAtPoint.Add(length);
            length += Vector3.Distance(transform.TransformPoint(points[i]), transform.TransformPoint(points[(i + 1) % points.Count]));
        }

        pathLength = length;
    }

    public void GetTransformAtDistance(float distance, out Vector3 position, out Quaternion rotation)
    {
        distance = ((distance % pathLength) + pathLength) % pathLength; // always positive

        int startPoint = (int)((distance / pathLength) * points.Count);

        while (startPoint + 1 < distanceAtPoint.Count && distanceAtPoint[startPoint] < distance)
            startPoint++;
        while (distanceAtPoint[startPoint] > distance)
            startPoint--;

        int nextPoint = (startPoint + 1) % points.Count;
        float blendFactor = startPoint < points.Count - 1 ? (distance - distanceAtPoint[startPoint]) / (distanceAtPoint[nextPoint] - distanceAtPoint[startPoint])
            : (distance - distanceAtPoint[startPoint]) / (pathLength - distanceAtPoint[startPoint]);

        position = transform.TransformPoint(Vector3.Lerp(points[startPoint], points[nextPoint], blendFactor));
        rotation = Quaternion.LookRotation(points[nextPoint] - points[startPoint]);
    }

    public Vector3 GetWorldPoint(int index)
    {
        return transform.TransformPoint(points[index]);
    }
}