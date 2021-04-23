using Mirror;
using UnityEngine;
using UnityEngine.AI;

public class AIController : MonoBehaviour
{
    public Path path;
    public float pathTargetAcceptanceRange = 2f;

    public int followPlayerId;

    private int currentTargetPathPoint = 0;

    private Character player;

    private PlayerInput input;

    private void Awake()
    {
        player = GetComponent<Character>();

        foreach (Spring spring in FindObjectsOfType<Spring>())
        {
            float springHeight = spring.springForce; // yeah sure whatever
            float springDist = 5f;

            for (float direction = 0; direction < 359.9f; direction += 45f)
            {
                if (NavMesh.SamplePosition(spring.transform.position + new Vector3(Mathf.Sin(direction * Mathf.Deg2Rad) * springDist, springHeight, Mathf.Cos(direction * Mathf.Deg2Rad) * springDist), out NavMeshHit hit, 100, ~0))
                {
                    NavMesh.AddLink(new NavMeshLinkData()
                    {
                        startPosition = spring.transform.position,
                        endPosition = hit.position,
                        costModifier = springHeight,
                    });
                }
            }
        }
    }

    private void Update()
    {
        if (NetworkServer.active)
        {
            input.horizontalAim = (input.horizontalAim + Time.deltaTime * 360f) % 360;

            Vector3 moveIntentionDirection = Vector3.zero;

            if (path)
            {
                if (Vector3.Distance(transform.position, path.GetWorldPoint(currentTargetPathPoint)) < pathTargetAcceptanceRange)
                {
                    currentTargetPathPoint = (currentTargetPathPoint + 1) % path.points.Count;
                }

                moveIntentionDirection = MoveTowardsTarget(path.GetWorldPoint(currentTargetPathPoint));
            }
            else if (Netplay.singleton.players.Count > followPlayerId && Netplay.singleton.players[followPlayerId])
            {
                moveIntentionDirection = MoveTowardsTarget(Netplay.singleton.players[followPlayerId].transform.position);
            }

            float sin = Mathf.Sin(-input.horizontalAim * Mathf.Deg2Rad);
            float cos = Mathf.Cos(-input.horizontalAim * Mathf.Deg2Rad);

            input.moveHorizontalAxis = moveIntentionDirection.x * cos + moveIntentionDirection.z * sin;
            input.moveVerticalAxis = -moveIntentionDirection.x * sin + moveIntentionDirection.z * cos;

            player.input = input;
            //controller.PushInput(input, Time.deltaTime);
            // we need to fix this somehow
        }
    }

    private Vector3 MoveTowardsTarget(Vector3 target)
    {
        Vector3 moveIntentionDirection = (target - transform.position).Horizontal().normalized;

        if (NavMesh.SamplePosition(target, out NavMeshHit targetHit, 10.0f, ~0))
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit myHit, 10.0f, ~0))
            {
                NavMeshPath path = new NavMeshPath();

                if (NavMesh.CalculatePath(myHit.position, targetHit.position, ~0, path) && path.corners.Length > 1)
                {
                    moveIntentionDirection = (path.corners[1] - transform.position).Horizontal().normalized;
                }
            }
        }

        return moveIntentionDirection;
    }
}
