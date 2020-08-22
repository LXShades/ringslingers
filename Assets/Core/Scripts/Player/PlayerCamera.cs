using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCamera : WorldObjectComponent
{
    /// <summary>
    /// Player we're currently following
    /// </summary>
    public Player currentPlayer;

    /// <summary>
    /// The height of the camera relative to the player's feet
    /// </summary>
    public float eyeHeight = 0.6f;

    /// <summary>
    /// The height (positive and negative) of eye bobs
    /// </summary>
    public float eyeBobHeight = 0.1f;

    /// <summary>
    /// The speed of eye bobs, in degrees per second
    /// </summary>
    public float eyeBobSpeed = 630;

    /// <summary>
    /// Maximum player velocity for maximum eye bob
    /// </summary>
    public float maxPlayerVelocityForEyeBob = 30;

    /// <summary>
    /// The max eye bob height when landing
    /// </summary>
    public float landEyeBobHeight = 0.3f;

    /// <summary>
    /// Maximum player landing speed for maximum eye bob
    /// </summary>
    public float maxPlayerLandForEyeBob = 30;

    private float lastPlayerFallSpeed = 0;

    private float landBobTimer = 0;
    private float landBobMagnitude = 0;
    private float landBobDuration = 0;

    public override void WorldAwake()
    {
    }

    public override void WorldLateUpdate(float deltaTime)
    {
        if (currentPlayer == null)
        {
            if (Netplay.singleton.localPlayerId >= 0)
                currentPlayer = worldObject.world.players[Netplay.singleton.localPlayerId]; // follow the player in this world
        }

        if (currentPlayer)
        {
            // Move to player position
            transform.position = currentPlayer.transform.position + Vector3.up * eyeHeight;
            transform.rotation = Quaternion.Euler(currentPlayer.input.verticalAim, currentPlayer.input.horizontalAim, 0);

            // Apply eye bob
            if (currentPlayer.movement.isOnGround)
            {
                if (lastPlayerFallSpeed > 0)
                {
                    landBobDuration = 0.4f;
                    landBobTimer = landBobDuration;
                    landBobMagnitude = landEyeBobHeight * Mathf.Min(lastPlayerFallSpeed / maxPlayerLandForEyeBob, 1);
                }

                if (landBobTimer > 0)
                {
                    float landProgress = 1 - (landBobTimer / landBobDuration);
                    transform.position += Vector3.up * (-landBobMagnitude * landProgress * 2 + landBobMagnitude * landProgress * landProgress * 2);
                    landBobTimer = Mathf.Max(landBobTimer - deltaTime, 0);
                }

                transform.position += Vector3.up * (Mathf.Sin(eyeBobSpeed * World.live.gameTime * Mathf.Deg2Rad) * eyeBobHeight * Mathf.Min(1, currentPlayer.movement.velocity.Horizontal().magnitude / maxPlayerVelocityForEyeBob));
            }

            lastPlayerFallSpeed = currentPlayer.movement.isOnGround ? 0 : Mathf.Max(-currentPlayer.movement.velocity.y, 0);
        }
        else
        {
            // spectator cam goes here?
        }
    }
}
