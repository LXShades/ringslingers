using System;
using UnityEngine;

[System.Serializable]
public struct PlayerInput : IEquatable<PlayerInput>
{
    // Movement
    public float moveHorizontalAxis
    {
        get => Compressor.BitsToUnitFloat(_moveHorizontalAxis, 8);
        set => _moveHorizontalAxis = (char)Compressor.UnitFloatToBits(value, 8);
    }
    public float moveVerticalAxis
    {
        get => Compressor.BitsToUnitFloat(_moveVerticalAxis, 8);
        set => _moveVerticalAxis = (char)Compressor.UnitFloatToBits(value, 8);
    }

    // Looking/camera
    public float horizontalAim
    {
        get => Compressor.BitsToUnitFloat(_horizontalAim, 16) * 360f;
        set => _horizontalAim = (short)Compressor.UnitFloatToBits(value / 360f, 16);
    }
    public float verticalAim
    {
        get => Compressor.BitsToUnitFloat(_verticalAim, 16) * 360f;
        set => _verticalAim = (short)Compressor.UnitFloatToBits(value / 360f, 16);
    }

    // Buttons
    public bool btnJump
    {
        get => (_buttons & 1) != 0;
        set => _buttons = (byte)(value ? (_buttons | 1) : (_buttons & ~1));
    }
    public bool btnFire
    {
        get => (_buttons & 2) != 0;
        set => _buttons = (byte)(value ? (_buttons | 2) : (_buttons & ~2));
    }

    // these aren't serialized
    public bool btnJumpPressed { get; set; }
    public bool btnJumpReleased { get; set; }
    public bool btnFirePressed { get; set; }
    public bool btnFireReleased { get; set; }

    // actual compressed data
    // 7 bytes
    public char _moveHorizontalAxis;
    public char _moveVerticalAxis;
    public short _horizontalAim;
    public short _verticalAim;
    public byte _buttons;

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

    /// <summary>
    /// Returns a copy with delta inputs (firePressed, fireReleased, etc) relative to lastInput
    /// </summary>
    public PlayerInput WithDeltas(PlayerInput lastInput)
    {
        PlayerInput output = this;

        output.btnJumpPressed = !lastInput.btnJump && btnJump;
        output.btnFirePressed = !lastInput.btnFire && btnFire;

        output.btnJumpReleased = lastInput.btnJump && !btnJump;
        output.btnFireReleased = lastInput.btnFire && !btnFire;

        return output;
    }

    /// <summary>
    /// Returns a copy with delta inputs (firePressed, fireReleased, etc) removed
    /// </summary>
    public PlayerInput WithoutDeltas()
    {
        PlayerInput output = this;

        output.btnFirePressed = output.btnFireReleased = false;
        output.btnJumpPressed = output.btnJumpReleased = false;

        return output;
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
public struct InputDelta
{
    public float time;
    public PlayerInput input;

    public InputDelta(float time, in PlayerInput input)
    {
        this.time = time;
        this.input = input;
    }
}

public struct InputPack
{
    public float extrapolation;
    public InputDelta[] inputs;
    public CharacterState state;

    /// <summary>
    /// Makes an InputPack
    /// </summary>
    /// <returns></returns>
    public static InputPack MakeFromHistory(HistoryList<PlayerInput> inputHistory, float sendBufferLength)
    {
        int startIndex = inputHistory.ClosestIndexBeforeOrEarliest(inputHistory.LatestTime - sendBufferLength);

        if (startIndex != -1)
        {
            InputDelta[] inputs = new InputDelta[startIndex + 1];
            for (int i = startIndex; i >= 0; i--)
            {
                inputs[i].time = inputHistory.TimeAt(i);
                inputs[i].input = inputHistory[i];
            }

            return new InputPack()
            {
                inputs = inputs
            };
        }

        return new InputPack()
        {
            inputs = new InputDelta[0]
        };
    }
}

public struct MoveStateWithInput
{
    public CharacterState state;
    public PlayerInput input;
}