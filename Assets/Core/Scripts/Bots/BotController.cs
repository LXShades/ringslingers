using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BotController : MonoBehaviour
{
    public interface IState
    {
        public void Update(BotController controller, Character character, ref PlayerInput input);
    }

    public Path path;
    public float pathTargetAcceptanceRange = 2f;

    public int followPlayerId;

    private int currentTargetPathPoint = 0;

    private Character character;

    private PlayerInput input;

    public List<IState> availableStates = new List<IState>();

    public List<IState> activeStates = new List<IState>();

    private void Awake()
    {
        availableStates.Add(new State_FollowPlayer());
        availableStates.Add(new State_MoveTowards());

        State_FollowPlayer moveState = GetOrActivateState<State_FollowPlayer>();
        moveState.followPlayerId = followPlayerId;
        GetOrActivateState<State_Spin>();
    }

    private void Update()
    {
        if (character == null)
        {
            Player myPlayer = GetComponent<Player>();

            if (myPlayer)
            {
                character = Netplay.singleton.players[myPlayer.playerId];

                if (character)
                    character.playerName = gameObject.name;
            }
        }

        if (NetworkServer.active && character)
        {
            for (int i = 0; i < activeStates.Count; i++)
                activeStates[i].Update(this, character, ref input);

            GameTicker.singleton.OnRecvBotInput(character.playerId, input);
        }
    }

    public TState GetOrActivateState<TState>() where TState : IState, new()
    {
        TState state = (TState)activeStates.Find(a => a.GetType() == typeof(TState));

        if (state == null)
            activeStates.Add(state = new TState());

        return state;
    }

    public class State_MoveTowards : IState
    {
        public Vector3 targetPosition;

        public bool canTravelToDestination { get; private set; } // false if blocked by something, possibly temporarily
        public bool hasReachedDestination { get; private set; }

        NavMeshPath path = new NavMeshPath();

        public void Update(BotController controller, Character character, ref PlayerInput input)
        {
            Vector3 nextPathPoint = GetNextPathPoint(character, targetPosition);
            Vector3 moveIntentionDirection = (nextPathPoint - character.transform.position).Horizontal().normalized;

            Debug.DrawLine(character.transform.position, nextPathPoint, Color.green);

            float sin = Mathf.Sin(-input.horizontalAim * Mathf.Deg2Rad);
            float cos = Mathf.Cos(-input.horizontalAim * Mathf.Deg2Rad);

            input.moveHorizontalAxis = moveIntentionDirection.x * cos + moveIntentionDirection.z * sin;
            input.moveVerticalAxis = -moveIntentionDirection.x * sin + moveIntentionDirection.z * cos;

            hasReachedDestination = Vector3.Distance(character.transform.position, targetPosition) <= 0.5f;
        }

        private Vector3 GetNextPathPoint(Character character, Vector3 target)
        {
            if (path.corners != null)
            {
                for (int i = 0; i + 1 < path.corners.Length; i++)
                    Debug.DrawLine(path.corners[i] + Vector3.up * 0.5f, path.corners[i + 1] + Vector3.up * 0.5f, Color.blue);
            }

            float tTo0 = character.movement.velocity.y / character.movement.gravity;
            float expectedPeakHeight = character.movement.transform.position.y + character.movement.velocity.y * tTo0 - (character.movement.gravity * tTo0 * tTo0 * 0.5f);

            if (NavMesh.SamplePosition(target, out NavMeshHit targetHit, 10.0f, ~0))
            {
                if (NavMesh.SamplePosition(character.transform.position, out NavMeshHit myHit, 10.0f, ~0))
                {
                    if (NavMesh.CalculatePath(myHit.position, targetHit.position, ~0, path) && path.corners.Length > 1 && path.status != NavMeshPathStatus.PathInvalid)
                    {
                        if (path.corners.Length > 2)
                        {
                            if (path.corners[2].y > path.corners[1].y + 0.1f) // i would love to just check if this part of the path is a link, but nope, unity won't allow that
                            {
                                // run towards a point a bit further back
                                if (Vector3.Dot(character.movement.velocity.normalized, (path.corners[2] - path.corners[1]).normalized) < 0.75f)
                                {
                                    return path.corners[1] - (path.corners[2] - path.corners[1]).Horizontal().normalized * 2f;
                                }
                                else if (character.movement.velocity.y > 1f || character.transform.position.y > path.corners[2].y)
                                {
                                    return path.corners[2];
                                }
                            }
                            else if (path.corners[2].y < path.corners[1].y - 0.1f)
                            {
                                // yeet into the lower area. don't wait around confusedly
                                return path.corners[2];
                            }
                        }

                        return path.corners[1];
                    }
                }
            }

            return Vector3.zero;
        }

        private float TimeToGetToPoint(CharacterMovement movement, Vector3 target)
        {
            // todo
            return 1f;
        }
    }

    public class State_FollowPlayer : IState
    {
        public int followPlayerId { get; set; }

        public void Update(BotController controller, Character character, ref PlayerInput input)
        {
            if (followPlayerId > 0 && Netplay.singleton.players.Count > followPlayerId && Netplay.singleton.players[followPlayerId])
            {
                controller.GetOrActivateState<State_MoveTowards>().targetPosition = Netplay.singleton.players[followPlayerId].transform.position;
            }

            /*if (path)
            {
                if (Vector3.Distance(character.transform.position, path.GetWorldPoint(currentTargetPathPoint)) < pathTargetAcceptanceRange)
                {
                    currentTargetPathPoint = (currentTargetPathPoint + 1) % path.points.Count;
                }

                moveIntentionDirection = MoveTowardsTarget(path.GetWorldPoint(currentTargetPathPoint));
            }
            else */
        }
    }

    public class State_Spin : IState
    {
        public void Update(BotController controller, Character character, ref PlayerInput input)
        {
            input.horizontalAim = (input.horizontalAim + Time.deltaTime * 360f) % 360f;
        }
    }
}
