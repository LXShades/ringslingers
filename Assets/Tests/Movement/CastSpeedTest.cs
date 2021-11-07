using UnityEngine;

public class CastSpeedTest : MonoBehaviour
{
    public int numCastsToCall = 100;
    public float spherecastMaxSize = 0.5f;
    public float castDistance = 1f;
    public Collider testAgainstCollider = null;

    void Update()
    {
        Vector3 pos = transform.position;
        long raycastUs, spherecastUs;
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (testAgainstCollider != null)
        {
            for (int i = 0; i < numCastsToCall; i++)
                testAgainstCollider.Raycast(new Ray(pos, Random.insideUnitSphere.normalized), out RaycastHit _, castDistance);
        }
        else
        {
            for (int i = 0; i < numCastsToCall; i++)
                Physics.Raycast(new Ray(pos, Random.insideUnitSphere.normalized), castDistance, ~0, QueryTriggerInteraction.Ignore);
        }

        stopwatch.Stop();
        raycastUs = stopwatch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency;
        stopwatch.Restart();

        for (int i = 0; i < numCastsToCall; i++)
            Physics.SphereCast(new Ray(pos, Random.insideUnitSphere.normalized), Random.value * spherecastMaxSize, castDistance, ~0, QueryTriggerInteraction.Ignore);

        stopwatch.Stop();
        spherecastUs = stopwatch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency;

        Debug.Log($"Ray: {raycastUs / 1000f}ms Sphere: {spherecastUs / 1000f}");
    }
}
