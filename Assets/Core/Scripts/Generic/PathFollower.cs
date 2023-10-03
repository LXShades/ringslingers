using Mirror;
using UnityEngine;

public class PathFollower : NetworkBehaviour
{
    public LinearPath path;
    public float speed = 5.0f;
    public float offset = 0f;

    public float frontBackWheelDistance = 2f;

    [SyncVar(hook = nameof(OnCurrentDistanceChanged))]
    private float currentDistance = 0;

    private float currentExtrapolatedDistance;

    private void Awake()
    {
        currentDistance = currentExtrapolatedDistance = offset;
    }

    private void Update()
    {
        currentExtrapolatedDistance = (currentExtrapolatedDistance + Time.deltaTime * speed) % path.pathLength;

        if (isServer)
            currentDistance = currentExtrapolatedDistance;

        Vector3 positionA, positionB;
        Quaternion rotation;

        path.GetTransformAtDistance(currentExtrapolatedDistance - frontBackWheelDistance * 0.5f, out positionA, out rotation);
        path.GetTransformAtDistance(currentExtrapolatedDistance + frontBackWheelDistance * 0.5f, out positionB, out rotation);

        transform.position = (positionA + positionB) * 0.5f;
        transform.rotation = Quaternion.LookRotation(positionB - positionA);
    }

    private void OnCurrentDistanceChanged(float distance, float newDistance)
    {
        currentDistance = currentExtrapolatedDistance = newDistance;
    }
}
