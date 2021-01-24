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
        set
        {
            horizontalAim = Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg;
            verticalAim = -Mathf.Asin(value.y) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Generates input commands from the current input
    /// </summary>
    /// <param name="lastInput"></param>
    /// <returns></returns>
    public static PlayerInput MakeLocalInput(PlayerInput lastInput, Vector3 up)
    {
        PlayerControls controls = GameManager.singleton.input;
        PlayerInput localInput = default;

        localInput.moveHorizontalAxis = controls.Gameplay.Movement.ReadValue<Vector2>().x;
        localInput.moveVerticalAxis = controls.Gameplay.Movement.ReadValue<Vector2>().y;

        Vector3 newAim = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * GamePreferences.mouseSpeed, up) * lastInput.aimDirection;
        // we need to clamp this...
        const float limit = 1f;
        float degreesFromUp = Mathf.Acos(Vector3.Dot(newAim, up)) * Mathf.Rad2Deg;
        float verticalAngleDelta = -Input.GetAxis("Mouse Y") * GamePreferences.mouseSpeed;

        if (degreesFromUp + verticalAngleDelta <= limit)
            verticalAngleDelta = limit - degreesFromUp;
        if (degreesFromUp + verticalAngleDelta >= 180f - limit)
            verticalAngleDelta = 180f - limit - degreesFromUp;
        newAim = Quaternion.AngleAxis(verticalAngleDelta, Vector3.Cross(up, newAim)) * newAim;

        if (controls.Gameplay.CenterCamera.ReadValue<float>() > 0.5f)
        {
            newAim.SetAlongAxis(up, 0);
            newAim.Normalize();
        }

        localInput.aimDirection = newAim;

        localInput.btnFire = controls.Gameplay.Fire.ReadValue<float>() > 0.5f; // seriously unity what the f***
        localInput.btnJump = controls.Gameplay.Jump.ReadValue<float>() > 0.5f; // this is apparently the way to read digital buttons, look it up

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
        writer.WriteInt16((short)(Compressor.UnitFloatToBits(moveHorizontalAxis, 8) | (Compressor.UnitFloatToBits(moveVerticalAxis, 8) << 8)));
        writer.WriteInt32((Compressor.UnitFloatToBits(horizontalAim / 360f, 16) | (Compressor.UnitFloatToBits(verticalAim / 360f, 16) << 16)));

        writer.WriteByte((byte)((btnJump ? 1 : 0) | (btnFire ? 2 : 0)));
    }

    public void Deserialize(NetworkReader reader)
    {
        short movement = reader.ReadInt16();
        int aim = reader.ReadInt32();

        moveHorizontalAxis = Compressor.BitsToUnitFloat(movement << 8 >> 8, 8);
        moveVerticalAxis = Compressor.BitsToUnitFloat(movement >> 8, 8);
        horizontalAim = Compressor.BitsToUnitFloat(aim << 16 >> 16, 16) * 360f;
        verticalAim = Compressor.BitsToUnitFloat(aim >> 16, 16) * 360f;

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