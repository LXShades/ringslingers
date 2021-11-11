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

    private Character character;

    private PlayerInput input;

    public List<IState> availableStates = new List<IState>();

    public List<IState> activeStates = new List<IState>();

    public string airNeuralNetworkString;
    public NeuralNetwork airNeuralNetwork { get; private set; }
    public string groundRunNeuralNetworkString;
    public NeuralNetwork groundRunNeuralNetwork { get; private set; }

    private void Awake()
    {
        availableStates.Add(new State_FollowPlayer());
        availableStates.Add(new State_MoveTowards());
        availableStates.Add(new State_GrabRings());

        airNeuralNetwork = new NeuralNetwork(new int[] { 6, 4, 2 });
        airNeuralNetwork.LoadAsString(airNeuralNetworkString);

        groundRunNeuralNetwork = new NeuralNetwork(new int[] { 6, 3, 2 });
        groundRunNeuralNetwork.LoadAsString(groundRunNeuralNetworkString);

        GetOrActivateState<State_GrabRings>();
        /*State_FollowPlayer moveState = GetOrActivateState<State_FollowPlayer>();
        moveState.followPlayerId = followPlayerId;
        GetOrActivateState<State_Spin>();*/
        
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
        public Vector3 targetPosition { get; private set; }

        public Vector3 nextTargetPosition { get; private set; }

        public bool useFastPath { get; private set; }

        public bool canTravelToDestination { get; private set; } // false if blocked by something, possibly temporarily
        public bool hasReachedDestination { get; private set; }

        NavMeshPath path = new NavMeshPath();

        private BotController botController;

        public void Update(BotController controller, Character character, ref PlayerInput input)
        {
            Vector3 nextPathPoint = GetNextPathPoint(character, targetPosition, out Vector3 recommendedAcceleration);
            Vector3 moveIntentionDirection = recommendedAcceleration == Vector3.zero ? (nextPathPoint - character.transform.position).Horizontal().normalized : recommendedAcceleration;

            Debug.DrawLine(character.transform.position, nextPathPoint, Color.green);

            float sin = Mathf.Sin(-input.horizontalAim * Mathf.Deg2Rad);
            float cos = Mathf.Cos(-input.horizontalAim * Mathf.Deg2Rad);

            input.moveHorizontalAxis = moveIntentionDirection.x * cos + moveIntentionDirection.z * sin;
            input.moveVerticalAxis = -moveIntentionDirection.x * sin + moveIntentionDirection.z * cos;

            hasReachedDestination = Vector3.Distance(character.transform.position, targetPosition) <= 0.5f;

            botController = controller;
        }

        public void SetTargetPosition(Vector3 targetPosition)
        {
            this.targetPosition = targetPosition;
            this.nextTargetPosition = targetPosition;
            this.useFastPath = false;
        }

        public void SetTargetPosition(Vector3 targetPosition, Vector3 nextTargetPosition)
        {
            this.targetPosition = targetPosition;
            this.nextTargetPosition = nextTargetPosition;
            this.useFastPath = true;
        }

        private Vector3 GetNextPathPoint(Character character, Vector3 target, out Vector3 recommendedAcceleration)
        {
            if (path.corners != null)
            {
                for (int i = 0; i + 1 < path.corners.Length; i++)
                    Debug.DrawLine(path.corners[i] + Vector3.up * 0.5f, path.corners[i + 1] + Vector3.up * 0.5f, Color.blue);
            }

            bool hasTargetPosition = NavMesh.SamplePosition(target, out NavMeshHit targetHit, 10.0f, ~0);
            bool hasLocalPosition = NavMesh.SamplePosition(character.transform.position, out NavMeshHit myHit, 20.0f, ~0);
            
            if (hasTargetPosition && hasLocalPosition)
                NavMesh.CalculatePath(myHit.position, targetHit.position, ~0, path);

            if (path.corners != null && path.corners.Length > 1 && path.status != NavMeshPathStatus.PathInvalid)
            {
                for (int i = path.corners.Length - 1; i > 0; i--)
                {
                    Vector3 point = path.corners[i];

                    bool shouldStopAtTarget = true;
                    if (i + 1 < path.corners.Length)
                    {
                        if (path.corners[i + 1].y > path.corners[i].y + 0.5f)
                        {
                            // PROBABLY A JUMP OR A SPRING
                            // check if we could make it with our current velocity and the spring
                            if (Vector3.Dot(character.movement.velocity.normalized, (path.corners[i + 1] - path.corners[i]).normalized) < 0.5f)
                            {
                                point = path.corners[i] - (path.corners[i + 1] - path.corners[i]).Horizontal().normalized * 2f;
                                shouldStopAtTarget = false;
                            }
                        }
                    }

                    if (i + 1 < path.corners.Length && path.corners[i + 1].y > path.corners[i].y + 0.5f)
                        shouldStopAtTarget = false; // we want speed

                    if (CanReachPoint(character.movement, character.transform.position, character.movement.velocity, point, nextTargetPosition, out recommendedAcceleration, shouldStopAtTarget))
                        return point;
                }

                recommendedAcceleration = Vector3.zero;
                return path.corners[1];
            }
            else
            {
                // skip straight to the target if we can
                if (CanReachPoint(character.movement, character.transform.position, character.movement.velocity, target, nextTargetPosition, out recommendedAcceleration, true))
                    return target;
            }

            // we can't really go anywhere, rip
            recommendedAcceleration = Vector3.zero;
            return character.transform.position;
        }

        private bool CanReachPoint(PlayerCharacterMovement movement, Vector3 position, Vector3 velocity, Vector3 target, Vector3 nextTarget, out Vector3 recommendedAcceleration, bool shouldStopAtTarget)
        {
            float deltaTime = 0.05f;
            float brakeFactor = 0.5f;

            recommendedAcceleration = Vector3.zero;
            Vector3 firstAcceleration = Vector3.zero;
            Vector3 prevPosition = position;

            for (int i = 0; i < 80; i++)
            {
                Vector3 directionToTarget = (target - position).Horizontal().normalized;
                Vector3 bestAcceleration = directionToTarget;
                float velocityDirectionDot = Vector3.Dot(directionToTarget, velocity.Horizontal().normalized);
                bool isOnGround = Physics.Raycast(position + new Vector3(0, movement.groundTestDistanceThreshold, 0), Vector3.down, movement.groundTestDistanceThreshold * 2, ~(1 << movement.gameObject.layer), QueryTriggerInteraction.Ignore);

                if (velocityDirectionDot >= 0f)
                {
                    bestAcceleration -= velocity.normalized * ((1f - velocityDirectionDot) * brakeFactor);
                    bestAcceleration.Normalize();
                }

                float horSpeed = velocity.Horizontal().magnitude;

                if (shouldStopAtTarget)
                {
                    // moderate speed so we don't overshoot target
                    float timeToDecelerateToZero = movement.inverseAccelCurve.Evaluate(horSpeed);
                    float timeToReachTargetAtCurrentSpeed = Vector3.Dot(velocity.Horizontal().normalized, target - position) / velocity.magnitude;
                    if (timeToDecelerateToZero > timeToReachTargetAtCurrentSpeed)
                    {
                        bestAcceleration.SetAlongAxis(velocity.Horizontal(), -1f);
                        bestAcceleration.Normalize();
                    }
                }

                if (!isOnGround && botController != null)
                {
                    if (botController.airNeuralNetwork != null)
                    {
                        float[] inputs = new float[]
                        {
                            target.x - position.x,
                            target.y - position.y,
                            target.z - position.z,
                            velocity.x,
                            velocity.y,
                            velocity.z
                        };
                        float[] outputs = botController.airNeuralNetwork.FeedForward(inputs);
                        bestAcceleration = new Vector3(outputs[0], 0f, outputs[1]).normalized;
                    }
                }
                if (isOnGround && botController != null && useFastPath)
                {
                    if (botController.groundRunNeuralNetwork != null)
                    {
                        float[] inputs = new float[]
                        {
                            target.x - position.x,
                            target.z - position.z,
                            nextTarget.x - position.x,
                            nextTarget.z - position.z,
                            velocity.x,
                            velocity.z
                        };
                        float[] outputs = botController.groundRunNeuralNetwork.FeedForward(inputs);
                        
                        bestAcceleration = new Vector3(outputs[0], 0f, outputs[1]).normalized;
                    }
                }

                // run accelerate
                float accelMultiplier = 0.5f;
                if (horSpeed < movement.topSpeed || Vector3.Dot(bestAcceleration, velocity) < 0f)
                    velocity += bestAcceleration * (movement.accelCurve.Evaluate(movement.inverseAccelCurve.Evaluate(velocity.Horizontal().magnitude) + deltaTime) - horSpeed) * accelMultiplier;

                // gravity
                velocity.y -= movement.gravity * deltaTime;

                if (Physics.Raycast(position, velocity, out RaycastHit hit, velocity.magnitude * deltaTime, ~0 & ~(1 << movement.gameObject.layer), QueryTriggerInteraction.Ignore))
                {
                    velocity.SetAlongAxis(hit.normal, 0f);
                    position += velocity * deltaTime;
                }
                else
                    position += velocity * deltaTime;

                if (i == 0)
                    firstAcceleration = bestAcceleration;

                Debug.DrawLine(prevPosition, position, Color.black);
                prevPosition = position;

                if (target.y > position.y + 0.5f)
                    return false;
                if (Vector3.Distance(position, target) < 0.5f)
                {
                    Debug.DrawLine(position - Vector3.up, position + Vector3.up, Color.green);
                    recommendedAcceleration = firstAcceleration;
                    return true;
                }
            }

            return false;
        }
    }

    public class State_FollowPlayer : IState
    {
        public int followPlayerId { get; set; }

        public void Update(BotController controller, Character character, ref PlayerInput input)
        {
            if (followPlayerId > 0 && Netplay.singleton.players.Count > followPlayerId && Netplay.singleton.players[followPlayerId])
            {
                controller.GetOrActivateState<State_MoveTowards>().SetTargetPosition(Netplay.singleton.players[followPlayerId].transform.position);
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

    public class State_GrabRings : IState
    {
        private List<Ring> rings = new List<Ring>();

        public void Update(BotController controller, Character character, ref PlayerInput input)
        {
            if (rings.Count == 0)
                rings.AddRange(FindObjectsOfType<Ring>());

            float closestDist = float.MaxValue;
            float nextClosestDist = float.MaxValue;
            Vector3 myPosition = character.transform.position;
            Vector3 closestPosition = myPosition;
            Vector3 nextClosestPosition = myPosition;

            foreach (Ring ring in rings)
            {
                float dist = Vector3.Distance(myPosition, ring.transform.position) + Mathf.Abs(ring.transform.position.y - myPosition.y) * 3f;
                if (ring.respawnableItem.isSpawned)
                {
                    if (dist < closestDist)
                    {
                        nextClosestDist = closestDist;
                        nextClosestPosition = closestPosition;

                        closestDist = dist;
                        closestPosition = ring.transform.position;
                    }
                    else if (dist < nextClosestDist)
                    {
                        nextClosestDist = dist;
                        nextClosestPosition = ring.transform.position;
                    }
                }
            }

            State_MoveTowards moveState = controller.GetOrActivateState<State_MoveTowards>();
            
            moveState.SetTargetPosition(closestPosition, nextClosestPosition);
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
