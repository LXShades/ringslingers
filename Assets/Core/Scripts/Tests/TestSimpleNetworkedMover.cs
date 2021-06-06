using Mirror;
using UnityEngine;

public class TestSimpleNetworkedMover : NetworkBehaviour
{
    public float movementCircleRadius = 10f;
    public float updatesPerSecond = 60f;
    public float movementSpeed = 1f;

    float lastUpdateTime;

    Vector3 initialPosition;

    private void Awake()
    {
        initialPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (isServer && updatesPerSecond > 0f && Time.time - lastUpdateTime > 1f / updatesPerSecond)
        {
            NextMove(transform.position = initialPosition + new Vector3(Mathf.Sin(Time.time * movementSpeed) * movementCircleRadius, 0f, Mathf.Cos(Time.time * movementSpeed) * movementCircleRadius));

            lastUpdateTime = Time.time;
        }
    }

    [ClientRpc(channel = Channels.Unreliable)]
    void NextMove(Vector3 position)
    {
        transform.position = position;
    }
}
