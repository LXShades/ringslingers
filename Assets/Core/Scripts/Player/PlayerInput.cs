using Mirror;
using System;
using UnityEngine;

[System.Serializable]
public struct PlayerInput : IEquatable<PlayerInput>
{
    public float moveHorizontalAxis;
    public float moveVerticalAxis;

    public float horizontalAim;
    public float verticalAim;

    public bool btnJump;
    public bool btnFire;

    public bool btnJumpPressed;
    public bool btnJumpReleased;
    public bool btnFirePressed;
    public bool btnFireReleased;

    public Vector3 aimDirection
    {
        get
        {
            float horizontalRads = horizontalAim * Mathf.Deg2Rad, verticalRads = verticalAim * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(horizontalRads) * Mathf.Cos(verticalRads), -Mathf.Sin(verticalRads), Mathf.Cos(horizontalRads) * Mathf.Cos(verticalRads));
        }
    }

    /// <summary>
    /// Generates input commands from the current input
    /// </summary>
    /// <param name="lastInput"></param>
    /// <returns></returns>
    public static PlayerInput MakeLocalInput(PlayerInput lastInput)
    {
        PlayerInput localInput = default;

        localInput.moveHorizontalAxis = Input.GetAxisRaw("Horizontal");
        localInput.moveVerticalAxis = Input.GetAxisRaw("Vertical");

        localInput.horizontalAim = (lastInput.horizontalAim + Input.GetAxis("Mouse X") % 360 + 360) % 360;
        localInput.verticalAim = Mathf.Clamp(lastInput.verticalAim - Input.GetAxis("Mouse Y"), -89.99f, 89.99f);

        localInput.btnFire = Input.GetButton("Fire");
        localInput.btnJump = Input.GetButton("Jump");

        return localInput;
    }

    public static PlayerInput MakeWithDeltas(PlayerInput input, PlayerInput lastInput)
    {
        PlayerInput output = input;

        output.btnJumpPressed = !lastInput.btnJump && input.btnJump;
        output.btnFirePressed = !lastInput.btnFire && input.btnFire;

        output.btnJumpReleased = lastInput.btnJump && !input.btnJump;
        output.btnFireReleased = lastInput.btnFire && !input.btnFire;

        return output;
    }

    public void Serialize(NetworkWriter writer)
    {
        writer.WriteSingle(moveHorizontalAxis);
        writer.WriteSingle(moveVerticalAxis);
        writer.WriteSingle(horizontalAim);
        writer.WriteSingle(verticalAim);
        writer.WriteByte((byte)((btnJump ? 1 : 0) | (btnFire ? 2 : 0)));
    }

    public void Deserialize(NetworkReader reader)
    {
        moveHorizontalAxis = reader.ReadSingle();
        moveVerticalAxis = reader.ReadSingle();
        horizontalAim = reader.ReadSingle();
        verticalAim = reader.ReadSingle();

        byte buttons = reader.ReadByte();
        btnJump = (buttons & 1) != 0;
        btnFire = (buttons & 2) != 0;
    }

    public bool Equals(PlayerInput other)
    {
        return moveHorizontalAxis == other.moveHorizontalAxis && moveVerticalAxis == other.moveVerticalAxis
            && horizontalAim == other.horizontalAim && verticalAim == other.verticalAim
            && btnFire == other.btnFire && btnJump == other.btnJump;
    }

    public override string ToString()
    {
        return $"H {moveHorizontalAxis:0.00} V {moveVerticalAxis:0.00} Jmp {btnJump}/P{btnJumpPressed}/R{btnJumpReleased} Fire {btnFire}/P{btnFirePressed}/R{btnFireReleased}";
    }
}

public static class PlayerInputSerializer
{
    public static void WritePlayerInput(this NetworkWriter writer, PlayerInput input)
    {
        input.Serialize(writer);
    }

    public static PlayerInput ReadPlayerInput(this NetworkReader reader)
    {
        PlayerInput output = new PlayerInput();

        output.Deserialize(reader);

        return output;
    }
}