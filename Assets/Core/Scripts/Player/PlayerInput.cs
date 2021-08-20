using System;
using UnityEngine;

[System.Serializable]
public struct PlayerInput : IEquatable<PlayerInput>, ITickerInput<PlayerInput>
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
    public bool btnSpin
    {
        get => (_buttons & 4) != 0;
        set => _buttons = (byte)(value ? (_buttons | 4) : (_buttons & ~4));
    }

    // these aren't serialized
    public bool btnJumpPressed { get; set; }
    public bool btnJumpReleased { get; set; }
    public bool btnFirePressed { get; set; }
    public bool btnFireReleased { get; set; }
    public bool btnSpinPressed { get; set; }
    public bool btnSpinReleased { get; set; }

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
        if (!GameManager.singleton.canPlayInputs)
            return lastInput; // no new inputs are being accepted

        PlayerControls controls = GameManager.singleton.input;
        PlayerInput localInput = default;

        localInput.moveHorizontalAxis = controls.Gameplay.Movement.ReadValue<Vector2>().x;
        localInput.moveVerticalAxis = controls.Gameplay.Movement.ReadValue<Vector2>().y;

        // mouselook
        if (GameManager.singleton.canPlayMouselook)
        {
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
        }
        else 
        {
            localInput.aimDirection = lastInput.aimDirection;
        }

        localInput.btnFire = GameManager.singleton.canPlayWeaponFire && controls.Gameplay.Fire.ReadValue<float>() > 0.5f; // seriously unity what the f***
        localInput.btnJump = controls.Gameplay.Jump.ReadValue<float>() > 0.5f; // this is apparently the way to read digital buttons, look it up
        localInput.btnSpin = controls.Gameplay.Spindash.ReadValue<float>() > 0.5f; // yeah these are all floating points I mean duh

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
        output.btnSpinPressed = !lastInput.btnSpin && btnSpin;

        output.btnJumpReleased = lastInput.btnJump && !btnJump;
        output.btnFireReleased = lastInput.btnFire && !btnFire;
        output.btnSpinReleased = lastInput.btnSpin && !btnSpin;

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
        output.btnSpinPressed = output.btnSpinReleased = false;

        return output;
    }

    public bool Equals(PlayerInput other)
    {
        return moveHorizontalAxis == other.moveHorizontalAxis && moveVerticalAxis == other.moveVerticalAxis
            && horizontalAim == other.horizontalAim && verticalAim == other.verticalAim
            && btnFire == other.btnFire && btnJump == other.btnJump && btnSpin == other.btnSpin;
    }

    public override string ToString()
    {
        return $"H {moveHorizontalAxis:0.00} V {moveVerticalAxis:0.00} " +
            $"Jump {btnJump}/P{btnJumpPressed}/R{btnJumpReleased} " +
            $"Fire {btnFire}/P{btnFirePressed}/R{btnFireReleased} " +
            $"Spin {btnSpin}/P{btnSpinPressed}/R{btnFireReleased}";
    }

    public PlayerInput GenerateLocal()
    {
        return new PlayerInput(); // hmm...
    }
}