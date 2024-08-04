using UnityEngine;
using System.Collections.Generic;

public class TestBotAction_Pathfinding : TestBotAction
{
    public float deltaTime = 0.0166666f;
    public float timePerIteration = 0.2f;
    public int maxNumIterations = 20;
    private float targetRadius;
    public bool shouldRunFullSimulation = true;

    private bool hasMadePath;

    private List<CharacterInput> inputs = new List<CharacterInput>();
    private int inputFrame = 0;

    public override void Init(TestBotExecutor exec)
    {
        hasMadePath = false;
        inputFrame = 0;
        inputs.Clear();
        deltaTime = exec.deltaTime;
        targetRadius = exec.targetRadius;
    }

    public override void Run(TestBotExecutor exec, ref CharacterInput input)
    {
        Vector3 endPosition = exec.targetPositions[exec.targetPositions.Count - 1];

        if (!hasMadePath)
        {
            Node start = new Node()
            {
                state = MakeState(exec),
            };
            start.state.velocity = exec.startVelocity;
            Node goal = new Node()
            {
                state = new CharacterState()
                {
                    position = endPosition
                },
            };

            AStar(exec, start, goal);
            input.worldMovementDirection = new Vector3(1f, 0f, 0f);
            exec.transform.position = start.state.position;
            exec.movement.velocity = start.state.velocity;

            hasMadePath = true;
        }

        int framesPerInput = Mathf.CeilToInt(timePerIteration / deltaTime);
        if (inputFrame < inputs.Count * framesPerInput)
        {
            input = inputs[inputFrame++ / framesPerInput];
        }
    }

    struct Node
    {
        public CharacterState state;
        public CharacterInput input;
        public float currentCost;
        public float predictedCost;
        public int cameFrom;
        public int reachedTargetIdx;
    }

    private List<Node> openNodes = new List<Node>(1024);
    private List<Node> visitedNodes = new List<Node>(1024);
    private List<float> totalDistanceRemainingPerGoal = new List<float>();

    private void AStar(TestBotExecutor exec, Node start, Node goal)
    {
        float[] turnActions = new[] { -180f, -60f, -30f, 0f, 30f, 60f };

        totalDistanceRemainingPerGoal.Clear();
        if (exec.targetPositions.Count > 0)
        {
            Vector3 pos = exec.targetPositions[exec.targetPositions.Count - 1];
            float dist = 0f;
            for (int i = exec.targetPositions.Count - 1; i >= 0; i--)
            {
                dist += Vector3.Distance(pos, exec.targetPositions[i]);
                pos = exec.targetPositions[i];
                totalDistanceRemainingPerGoal.Add(dist); // equiv to Insert(0, dist)
            }
            totalDistanceRemainingPerGoal.Reverse();
        }

        start.predictedCost = Heuristic(exec, start, goal);
        start.currentCost = 0f;
        start.input = default;
        start.cameFrom = -1;
        start.reachedTargetIdx = -1;

        visitedNodes.Clear();
        openNodes.Clear();
        openNodes.Add(start);

        int numIterations = 0;
        while (openNodes.Count > 0 && numIterations++ < maxNumIterations)
        {
            int nextNodeIdx = openNodes.Count - 1;
            Node currentNode = openNodes[nextNodeIdx];
            openNodes.RemoveAt(nextNodeIdx); // reduce copies by taking the highest item in the list

            int currentNodeIdx = visitedNodes.Count;
            visitedNodes.Add(currentNode);

            if (currentNode.reachedTargetIdx + 1 >= exec.targetPositions.Count)
            {
                continue;
            }

            for (int action = 0; action < turnActions.Length; action++)
            {
                Vector3 defaultForwardDirection = (exec.targetPositions[currentNode.reachedTargetIdx + 1] - currentNode.state.position).normalized;
                Node nextNode = currentNode;
                int actionIterations = Mathf.CeilToInt(timePerIteration / deltaTime);
                float sin = Mathf.Sin(turnActions[action] * Mathf.Deg2Rad);
                float cos = Mathf.Cos(turnActions[action] * Mathf.Deg2Rad);
                Vector3 currentMovementDirectionOrDefault = currentNode.state.velocity.magnitude > 0.5f ? currentNode.state.velocity : defaultForwardDirection;
                Vector3 worldInputDirection = new Vector3(currentMovementDirectionOrDefault.x * cos - currentMovementDirectionOrDefault.z * sin, 0f, currentMovementDirectionOrDefault.x * sin + currentMovementDirectionOrDefault.z * cos).normalized;
                nextNode.input.worldMovementDirection = worldInputDirection;

                if (shouldRunFullSimulation)
                {
                    exec.movement.transform.position = currentNode.state.position;
                    exec.movement.velocity = currentNode.state.velocity;
                }

                for (int actionIt = 0; actionIt < actionIterations; actionIt++)
                {
                    if (shouldRunFullSimulation)
                    {
                        exec.movement.TickMovement(deltaTime, nextNode.input);
                        nextNode.state.position = exec.transform.position;
                        nextNode.state.velocity = exec.movement.velocity;
                    }
                    else
                    {
                        exec.movement.RunSimpleCollisionFreeSimulation(ref nextNode.state, in nextNode.input, deltaTime);
                    }

                    if (nextNode.reachedTargetIdx + 1 < exec.targetPositions.Count)
                    {
                        if (VectorExtensions.HorizontalDistance(nextNode.state.position, exec.targetPositions[nextNode.reachedTargetIdx + 1]) <= targetRadius)
                        {
                            nextNode.reachedTargetIdx++;
                        }
                    }
                }

                float distanceToNext = timePerIteration;

                nextNode.currentCost = currentNode.currentCost + distanceToNext;
                nextNode.predictedCost = currentNode.currentCost + (nextNode.reachedTargetIdx + 1 < exec.targetPositions.Count ? Heuristic(exec, nextNode, goal) : 0f);
                nextNode.cameFrom = currentNodeIdx;

                if (openNodes.Count >= 2 && openNodes[0].predictedCost != openNodes[openNodes.Count - 1].predictedCost)
                {
                    // 'sort' it into the list approximately
                    float lowestFscore = openNodes[openNodes.Count - 1].predictedCost;
                    float highestFscore = openNodes[0].predictedCost;
                    openNodes.Insert(Mathf.Clamp(openNodes.Count - Mathf.RoundToInt(((nextNode.predictedCost - lowestFscore) / (highestFscore - lowestFscore)) * openNodes.Count), 0, openNodes.Count), nextNode);
                }
                else
                {
                    if (openNodes.Count > 0 && nextNode.predictedCost < openNodes[openNodes.Count - 1].predictedCost)
                        openNodes.Add(nextNode);
                    else
                        openNodes.Insert(0, nextNode);
                }
            }
        }

        // reconstruct the path from the last visited node, that was the last best one we checked
        if (visitedNodes.Count > 0)
        {
            // Find the complete path with the lowest total cost
            int highestTargetIdx = 0;
            float lowestCost = 99999f;
            int bestNodeIdx = 0;

            for (int nodeIdx = 0; nodeIdx <  visitedNodes.Count; nodeIdx++)
            {
                var node = visitedNodes[nodeIdx];
                if (node.reachedTargetIdx > highestTargetIdx || (node.reachedTargetIdx == highestTargetIdx && node.currentCost < lowestCost))
                {
                    lowestCost = node.currentCost;
                    highestTargetIdx = node.reachedTargetIdx;
                    bestNodeIdx = nodeIdx;
                }
            }

            int currentNodeIdx = bestNodeIdx;
            while (currentNodeIdx != -1)
            {
                inputs.Add(visitedNodes[currentNodeIdx].input);
                currentNodeIdx = visitedNodes[currentNodeIdx].cameFrom;
            }
            inputs.Reverse();
        }
    }

    public float maxCostColor = 20f;

    public float heuristicMultiplier = 1f;
    private float Heuristic(TestBotExecutor exec, in Node node, in Node goal)
    {
        int targetPosition = node.reachedTargetIdx + 1;
        return (totalDistanceRemainingPerGoal[targetPosition] + Vector3.Distance(node.state.position, exec.targetPositions[targetPosition])) * heuristicMultiplier;
    }

    public override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        foreach (var node in visitedNodes)
        {
            if (node.cameFrom != -1)
            {
                Gizmos.color = Color.Lerp(Color.white, Color.red, node.predictedCost / maxCostColor);
                Gizmos.DrawLine(node.state.position + new Vector3(0f, 0.05f, 0f), visitedNodes[node.cameFrom].state.position + new Vector3(0f, 0.05f, 0f));
            }
        }
    }

    public static CharacterState MakeState(TestBotExecutor exec)
    {
        return new CharacterState()
        {
            position = exec.transform.position,
            rotation = exec.transform.rotation,
            state = exec.movement.state,
            velocity = exec.movement.velocity,
            up = exec.movement.up,
            stateFloat = exec.movement.stateFloat
        };
    }

    /// <summary>
    /// Applies a state package to our actual state
    /// </summary>
    /// <param name="state"></param>
    public static void ApplyState(TestBotExecutor exec, CharacterState state)
    {
        exec.transform.position = state.position;
        exec.transform.rotation = state.rotation;
        exec.movement.state = state.state;
        exec.movement.velocity = state.velocity;
        exec.movement.up = state.up;
        exec.movement.stateFloat = state.stateFloat;

        Physics.SyncTransforms(); // CRUCIAL for correct collision checking - a lot of things broke before adding this...
    }
}