using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BotController : MonoBehaviour
{
    public interface IState
    {
        public void Update(BotController controller, Character character, ref CharacterInput input);
    }

    public Path path;
    public float pathTargetAcceptanceRange = 2f;

    public int followPlayerId;

    private Character character;

    private CharacterInput lastInput;

    public List<IState> activeStates = new List<IState>();

    public string airNeuralNetworkString;
    public NeuralNetwork airNeuralNetwork { get; private set; }
    public string groundRunNeuralNetworkString;
    public NeuralNetwork groundRunNeuralNetwork { get; private set; }

    private void Awake()
    {
        airNeuralNetwork = new NeuralNetwork(new int[] { 6, 4, 2 });
        airNeuralNetwork.LoadAsString(airNeuralNetworkString);

        groundRunNeuralNetwork = new NeuralNetwork(new int[] { 6, 3, 2 });
        groundRunNeuralNetwork.LoadAsString(groundRunNeuralNetworkString);

        GetOrActivateState<State_CollectAndShoot>();
        
    }

    public void OnInputTick()
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
                activeStates[i].Update(this, character, ref lastInput);

            lastInput = lastInput.WithDeltas(character.entity.latestInput);
            GameTicker.singleton.OnRecvBotInput(character.playerId, lastInput);
        }
    }

    public TState GetOrActivateState<TState>() where TState : IState, new()
    {
        TState state = (TState)activeStates.Find(a => a.GetType() == typeof(TState));

        if (state == null)
            activeStates.Add(state = new TState());

        return state;
    }

    public void DeactivateState<TState>() where TState : IState
    {
        activeStates.RemoveAll(a => a.GetType() == typeof(TState));
    }

    public void ClearStates()
    {
        activeStates.Clear();
    }

    public class State_CollectAndShoot : IState
    {
        private bool isCollecting = true;

        private int numRingsToStartShooting = 10;
        private int numRingsToStartCollecting = 1;

        private float maxRangeForNonRails = 25f;
        private float maxRangeForRails = 50f;

        public void Update(BotController controller, Character character, ref CharacterInput input)
        {
            if (!isCollecting)
            {
                Character target = SelectTarget(character, out bool targetIsVisible);

                controller.DeactivateState<State_CollectRings>();

                if (target)
                {
                    CharacterShooting shooting = character.GetComponent<CharacterShooting>();

                    bool isRail = shooting.equippedWeapons.Contains(shooting.wepKeyRail);
                    bool targetIsInRange = isRail && Vector3.Distance(character.transform.position, target.transform.position) < maxRangeForRails
                        || !isRail && Vector3.Distance(character.transform.position, target.transform.position) < maxRangeForNonRails;

                    if (!targetIsVisible || !targetIsInRange)
                    {
                        controller.GetOrActivateState<State_FollowPlayer>().followPlayerId = target.playerId;
                    }
                    else if (targetIsVisible)
                    {
                        controller.DeactivateState<State_FollowPlayer>();
                        TryShootTarget(character, target, ref input);
                    }
                }
                else
                {
                    isCollecting = true;
                }

                if (character.numRings <= numRingsToStartCollecting)
                    isCollecting = true;
            }

            if (isCollecting)
            {
                controller.DeactivateState<State_FollowPlayer>();

                controller.GetOrActivateState<State_CollectRings>();

                if (character.numRings >= numRingsToStartShooting)
                    isCollecting = false;
            }
        }

        private Character SelectTarget(Character myCharacter, out bool targetIsVisible)
        {
            float closestVisibleCharDistance = float.MaxValue;
            Character closestVisibleChar = null;
            float closestCharDistance = float.MaxValue;
            Character closestChar = null;

            foreach (Character character in Netplay.singleton.players)
            {
                if (character && character != myCharacter && character.damageable.CanBeDamagedBy(myCharacter.damageable.damageTeam))
                {
                    float distance = Vector3.Distance(character.transform.position, myCharacter.transform.position);
                    
                    if (distance < closestCharDistance)
                    {
                        closestChar = character;
                        closestCharDistance = distance;
                    }

                    PhysicsExtensions.Parameters parameters = default;
                    parameters.ignoreObject = myCharacter.gameObject;
                    if (!PhysicsExtensions.Raycast(myCharacter.transform.position, character.transform.position - myCharacter.transform.position, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore, parameters)
                        || hit.collider.gameObject == character.gameObject)
                    {
                        if (distance < closestVisibleCharDistance)
                        {
                            closestVisibleChar = character;
                            closestVisibleCharDistance = distance;
                        }
                    }
                }
            }

            if (closestVisibleChar)
            {
                targetIsVisible = true;
                return closestVisibleChar;
            }
            else
            {
                targetIsVisible = false;
                return closestChar;
            }
        }

        private void TryShootTarget(Character myCharacter, Character target, ref CharacterInput playerInput)
        {
            CharacterShooting shooting = myCharacter.GetComponent<CharacterShooting>();
            bool isRail = shooting.equippedWeapons.Contains(shooting.wepKeyRail);
            bool tryThrowRing = false;

            if (isRail)
            {
                playerInput.aimDirection = (target.transform.position - myCharacter.transform.position).normalized;
                tryThrowRing = true;
            }
            else
            {
                if (shooting.PredictTargetPosition(target, out Vector3 predictedPosition, 2f))
                {
                    playerInput.aimDirection = (predictedPosition - myCharacter.transform.position).normalized;
                    tryThrowRing = true;
                }
                else
                {
                    playerInput.aimDirection = (target.transform.position - myCharacter.transform.position).normalized;
                }
            }

            if (shooting.CanThrowRing(0f) && tryThrowRing)
            {
                shooting.LocalThrowRing();
            }
        }
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

        public void Update(BotController controller, Character character, ref CharacterInput input)
        {
            Vector3 nextPathPoint = GetNextPathPoint(character, targetPosition, out Vector3 recommendedAcceleration);
            Vector3 moveIntentionDirection = recommendedAcceleration == Vector3.zero ? (nextPathPoint - character.transform.position).Horizontal().normalized : recommendedAcceleration;

            DebugDraw.DrawLine(character.transform.position, nextPathPoint, Color.green);

            float sin = Mathf.Sin(-input.horizontalAim * Mathf.Deg2Rad);
            float cos = Mathf.Cos(-input.horizontalAim * Mathf.Deg2Rad);

            input.moveHorizontalAxis = moveIntentionDirection.x * cos + moveIntentionDirection.z * sin;
            input.moveVerticalAxis = -moveIntentionDirection.x * sin + moveIntentionDirection.z * cos;

            // jump up places
            bool canJumpLedge = nextPathPoint.y > character.transform.position.y + 0.1f;
            bool isAlreadyJumping = !character.movement.isOnGround;
            bool needsToHoldJump = (!character.movement.isOnGround && character.movement.velocity.y > 0f);
            bool hasAlreadyReleasedButton = character.movement.state == CharacterMovementState.JumpedReleasedButton; // so we don't thok

            if (((canJumpLedge && !isAlreadyJumping) || needsToHoldJump) && !hasAlreadyReleasedButton)
                input.btnJump = true;
            else
                input.btnJump = false;

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
            Color navmeshCastedColor = Color.red;
            Color pathColor = Color.blue;
            bool hasTargetPosition = NavMesh.SamplePosition(target, out NavMeshHit targetHit, 10.0f, ~0);
            bool hasLocalPosition = NavMesh.SamplePosition(character.transform.position, out NavMeshHit myHit, 20.0f, ~0);
            
            if (hasTargetPosition && hasLocalPosition)
                NavMesh.CalculatePath(myHit.position, targetHit.position, ~0, path);

            if (path.corners != null)
            {
                for (int i = 0; i + 1 < path.corners.Length; i++)
                    DebugDraw.DrawLine(path.corners[i] + Vector3.up * 0.5f, path.corners[i + 1] + Vector3.up * 0.5f, pathColor);
                DebugDraw.DrawLine(path.corners[path.corners.Length - 1], target, pathColor);
            }

            if (hasTargetPosition)
            {
                DebugDraw.DrawCross(targetHit.position, 1f, navmeshCastedColor);
                DebugDraw.DrawLine(targetHit.position, target, navmeshCastedColor);
            }

            if (path.corners != null && path.corners.Length > 1 && path.status != NavMeshPathStatus.PathInvalid)
            {
                for (int i = path.corners.Length - 1; i > 0; i--)
                {
                    Vector3 point = path.corners[i];

                    bool shouldStopAtTarget = false;

                    // originally the jump/spring check was here
                    // INSTEAD: we should pre-plan the path and visualise it to determine where we need acceleration/speed to move, and where we don't


                    if (CanSimulateMovementToPoint(character.movement, character.transform.position, character.movement.velocity, point, nextTargetPosition, out recommendedAcceleration, shouldStopAtTarget))
                        return point;
                }

                recommendedAcceleration = Vector3.zero;
                return path.corners[1];
            }
            else
            {
                // skip straight to the target if we can
                if (CanSimulateMovementToPoint(character.movement, character.transform.position, character.movement.velocity, target, nextTargetPosition, out recommendedAcceleration, true))
                    return target;
            }

            // we can't really go anywhere, rip
            recommendedAcceleration = Vector3.zero;
            return character.transform.position;
        }

        private bool CanSimulateMovementToPoint(PlayerCharacterMovement movement, Vector3 position, Vector3 velocity, Vector3 target, Vector3 nextTarget, out Vector3 recommendedAcceleration, bool shouldStopAtTarget)
        {
            if (Vector3.Distance(target, position) < 3f)
            {
                // nvm
                recommendedAcceleration = Vector3.zero; // no idea
                return true;
            }

            // Let's try computing multiple approaches:
            // -> Get there without a jump
            // -> Go there with a thok
            // -> Go there without a running start?

            float deltaTime = 0.05f;
            float brakeFactor = 0.5f;

            recommendedAcceleration = Vector3.zero;
            Vector3 firstAcceleration = Vector3.zero;
            Vector3 prevPosition = position;

            velocity.y = movement.jumpSpeed;

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

                DebugDraw.DrawLine(prevPosition, position, Color.black);
                prevPosition = position;

                if (Vector3.Distance(position, target) < 0.5f)
                {
                    DebugDraw.DrawLine(position - Vector3.up, position + Vector3.up, Color.green);
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

        public void Update(BotController controller, Character character, ref CharacterInput input)
        {
            if (Netplay.singleton.players.Count > followPlayerId && Netplay.singleton.players[followPlayerId])
                controller.GetOrActivateState<State_MoveTowards>().SetTargetPosition(Netplay.singleton.players[followPlayerId].transform.position);
            else
                controller.DeactivateState<State_MoveTowards>();
        }
    }

    public class State_CollectRings : IState
    {
        private List<Ring> rings = new List<Ring>();

        public bool doExcludeWeapons = true;

        public void Update(BotController controller, Character character, ref CharacterInput input)
        {
            if (rings.Count == 0)
                rings.AddRange(FindObjectsByType<Ring>(FindObjectsSortMode.None));

            float closestDist = float.MaxValue;
            float nextClosestDist = float.MaxValue;
            Vector3 myPosition = character.transform.position;
            Vector3 closestPosition = myPosition;
            Vector3 nextClosestPosition = myPosition;
            State_MoveTowards moveState = controller.GetOrActivateState<State_MoveTowards>();

            foreach (Ring ring in rings)
            {
                if (ring && (!doExcludeWeapons || !ring.GetComponent<RingWeaponPickup>()))
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
            }
            
            moveState.SetTargetPosition(closestPosition, nextClosestPosition);
        }
    }

    public class State_CopyPlayer : IState
    {
        public int playerIndex;

        private TimelineTrack<CharacterInput> targetInputs = new TimelineTrack<CharacterInput>();

        public void Update(BotController controller, Character character, ref CharacterInput input)
        {
            if (Netplay.singleton.players[playerIndex])
            {
                State_MoveTowards moveState = controller.GetOrActivateState<State_MoveTowards>();
                Vector3 targetPosition = Netplay.singleton.players[playerIndex].transform.position;
                CharacterInput playerInput = Netplay.singleton.players[playerIndex].liveInput;

                targetInputs.Insert(Time.timeAsDouble, playerInput);
                targetInputs.TrimBefore(Time.timeAsDouble - 1f);

                targetPosition.z = -targetPosition.z;

                playerInput = targetInputs[targetInputs.Count - 1];
                input.btnFire = playerInput.btnFire;
                input.btnJump = playerInput.btnJump;
                input.btnSpin = playerInput.btnSpin;

                moveState.SetTargetPosition(targetPosition);
            }
        }
    }

    public class State_Spin : IState
    {
        public void Update(BotController controller, Character character, ref CharacterInput input)
        {
            input.horizontalAim = (input.horizontalAim + Time.deltaTime * 360f) % 360f;
        }
    }
}
