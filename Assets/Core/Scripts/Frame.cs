using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Frame
{
    public static Frame current
    {
        get
        {
            if (_current == null)
            {
                _current = GameManager.singleton.currentFrame;
            }

            return _current;
        }
    }
    private static Frame _current;

    // Game time
    /// <summary>
    /// The delta time of the current tick
    /// </summary>
    public float deltaTime;

    /// <summary>
    /// The time at the current tick
    /// </summary>
    public float time;

    /// <summary>
    /// The local player's inputs at this tick
    /// </summary>
    public InputCmds localInput = new InputCmds();

    /// <summary>
    /// Advances the game by the given delta time
    /// </summary>
    /// <param name="deltaTime"></param>
    public void Advance(float deltaTime)
    {
        // Update timing
        this.deltaTime = deltaTime;
        time = time + deltaTime;

        // Update player inputs
        localInput.moveHorizontalAxis = Input.GetAxis("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxis("Vertical");

        localInput.lookHorizontalAxis = Input.GetAxis("Mouse X");
        localInput.lookVerticalAxis = -Input.GetAxis("Mouse Y");

        // Update game objects
        foreach (SyncedObject obj in GameObject.FindObjectsOfType<SyncedObject>())
        {
            obj.TriggerStartIfCreated();
            obj.FrameUpdate();
        }

        foreach (SyncedObject obj in GameObject.FindObjectsOfType<SyncedObject>())
        {
            obj.FrameLateUpdate();
        }
    }

    /// <summary>
    /// Rewinds the gamestate to an earlier snapshot
    /// </summary>
    /// <param name="state"></param>
    public void Rewind(GameState state)
    {

    }
}

public class InputCmds
{
    public float moveHorizontalAxis;
    public float moveVerticalAxis;

    public float lookHorizontalAxis;
    public float lookVerticalAxis;
}