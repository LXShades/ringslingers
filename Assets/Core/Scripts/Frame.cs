using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Frame
{
    /// <summary>
    /// The current game frame according to current simulations
    /// </summary>
    public static Frame local
    {
        get
        {
            if (_current == null)
            {
                _current = GameManager.singleton.localFrame;
            }

            return _current;
        }
    }
    private static Frame _current;

    /// <summary>
    /// The actual physical game frame
    /// </summary>
    public static Frame server
    {
        get
        {
            if (_server == null)
            {
                _server = GameManager.singleton.serverFrame;
            }

            return _server;
        }
    }
    private static Frame _server;


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
    /// Fixed delta time running at the server tick rate
    /// </summary>
    public const float serverDeltaTime = 0.1f;

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
        localInput.moveHorizontalAxis = Input.GetAxisRaw("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxisRaw("Vertical");

        localInput.lookHorizontalAxis = Input.GetAxis("Mouse X");
        localInput.lookVerticalAxis = -Input.GetAxis("Mouse Y");

        // Update game objects
        foreach (SyncedObject obj in GameManager.singleton.syncedObjects)
        {
            obj.TriggerStartIfCreated();
            obj.FrameUpdate();
        }

        foreach (SyncedObject obj in GameManager.singleton.syncedObjects)
        {
            obj.FrameLateUpdate();
        }
    }
}

public class InputCmds
{
    public float moveHorizontalAxis;
    public float moveVerticalAxis;

    public float lookHorizontalAxis;
    public float lookVerticalAxis;
}