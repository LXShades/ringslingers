using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class TestBotBehaviours : MonoBehaviour
{
    [Header("Settings")]
    [Range(0f, 50f)]
    public float targetTime;
    public float deltaTime = 0.166666f;

    public Vector3 targetPosition;

    private float currentTime;

    private PlayerCharacterMovement movement;

    [Header("Run")]
    public bool runNow;
    public bool runConstantly;

    [Header("Output")]
    public float timeTaken;

    private List<Tuple<Vector3, Quaternion>> positionHistory = new List<Tuple<Vector3, Quaternion>>();

    private void Update()
    {
        if (!Application.isPlaying)
        {
            if (runNow)
            {
                runNow = false;
                Run();
            }

            if (runConstantly)
                Run();
        }
    }

    private void Run()
    {
        movement = GetComponent<PlayerCharacterMovement>();
        positionHistory.Clear();

        currentTime = 0f;

        CharacterState initialState = new CharacterState()
        {
            position = transform.position,
            rotation = transform.rotation,
            state = movement.state,
            velocity = movement.velocity, 
            up = movement.up,
            stateFloat = movement.stateFloat
        };

        try
        {
            for (currentTime = 0f; currentTime < targetTime; currentTime += deltaTime)
            {
                Simulate();
                positionHistory.Add(new Tuple<Vector3, Quaternion>(transform.position, transform.rotation));
            }
        }
        finally
        {
            transform.position = initialState.position;
            transform.rotation = initialState.rotation;
            movement.state = initialState.state;
            movement.velocity = initialState.velocity;
            movement.up = initialState.up;
            movement.stateFloat = initialState.stateFloat;
        }

        timeTaken = currentTime;
    }

    private void Simulate()
    {
        Vector3 moveIntentionDirection = targetPosition - transform.position;
        Vector3 intendedAim = Vector3.forward;

        CharacterInput input = new CharacterInput()
        {
            aimDirection = intendedAim,
        };

        float sin = Mathf.Sin(-input.horizontalAim * Mathf.Deg2Rad);
        float cos = Mathf.Cos(-input.horizontalAim * Mathf.Deg2Rad);
        Vector2 moveAxes = new Vector2(moveIntentionDirection.x * cos + moveIntentionDirection.z * sin, -moveIntentionDirection.x * sin + moveIntentionDirection.z * cos).normalized;
        input.moveHorizontalAxis = moveAxes.x;
        input.moveVerticalAxis = moveAxes.y;

        movement.TickMovement(deltaTime, input);
    }

    private void OnDrawGizmos()
    {
        const int numColors = 3;
        Color[] colorsPerSecond = new Color[3] { Color.red, Color.green, Color.blue };

        int numPositions = positionHistory.Count;
        for (int i = 0; i < numPositions - 1; i++)
        {
            Gizmos.color = colorsPerSecond[(int)(i * deltaTime) % numColors];
            Gizmos.DrawLine(positionHistory[i].Item1, positionHistory[i + 1].Item1);
        }
    }
}
