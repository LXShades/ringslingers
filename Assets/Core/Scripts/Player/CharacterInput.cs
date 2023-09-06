using System;
using UnityEngine;

[System.Serializable]
public struct CharacterInput : IEquatable<CharacterInput>, ITickerInput<CharacterInput>
{
    // Movement
    public float moveHorizontalAxis
    {
        get => Compressor.BitsToUnitFloat(_moveAxes & 0x00FF, 8);
        set => _moveAxes = (ushort)(Compressor.UnitFloatToBits(value, 8) | (_moveAxes & ~0x00FF));
    }
    public float moveVerticalAxis
    {
        get => Compressor.BitsToUnitFloat((_moveAxes & 0xFF00) >> 8, 8);
        set => _moveAxes = (ushort)((Compressor.UnitFloatToBits(value, 8) << 8) | (_moveAxes & ~0xFF00));
    }

    // Looking/camera
    public float horizontalAim
    {
        get => Compressor.BitsToUnitFloat((int)(_aim & 0x0000FFFF), 16) * 360f;
        set => _aim = (uint)((uint)Compressor.UnitFloatToBits(value / 360f, 16) | (_aim & ~0x0000FFFF));
    }
    public float verticalAim
    {
        get => Compressor.BitsToUnitFloat((int)((_aim & 0xFFFF0000) >> 16), 16) * 360f;
        set => _aim = (uint)(Compressor.UnitFloatToBits(value / 360f, 16) << 16) | (_aim & ~0xFFFF0000);
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
    public ushort _moveAxes; // HHVV where H= horizontal compressed and V = vertical compressed
    public uint _aim; // HHHHVVVV where H=horizontal compressed and V = vertical compressed
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
    /// <returns></returns>
    public static CharacterInput MakeLocalInput(CharacterInput lastInput)
    {
        GameManager gm = GameManager.singleton;
        if (!gm.canPlayInputs)
        {
            gm.ClearBufferedInputs();
            return lastInput; // no new inputs are being accepted
        }

        PlayerControls controls = gm.input;
        CharacterInput localInput = default;

        localInput.moveHorizontalAxis = controls.Gameplay.Movement.ReadValue<Vector2>().x;
        localInput.moveVerticalAxis = controls.Gameplay.Movement.ReadValue<Vector2>().y;

        // mouselook
        if (gm.camera && Netplay.singleton && gm.camera.currentPlayer == Netplay.singleton.localPlayer)
            localInput.aimDirection = gm.camera.aimDirection;
        else
            localInput.aimDirection = lastInput.aimDirection;

        // we use buffered inputs because some inputs could run in-between fixed ticks, and therefore never happen. this is particularly true for mouse wheel events which only last one frame
        localInput.btnFire = gm.canPlayWeaponFire && (controls.Gameplay.Fire.ReadValue<float>() > 0f || gm.bufferedLocalBtnFire);
        localInput.btnJump = gm.bufferedLocalBtnJump || controls.Gameplay.Jump.ReadValue<float>() > 0f;
        localInput.btnSpin = gm.bufferedLocalBtnSpin || controls.Gameplay.Spindash.ReadValue<float>() > 0f;

        gm.ClearBufferedInputs();
        return localInput;
    }

    /// <summary>
    /// Returns a copy with delta inputs (firePressed, fireReleased, etc) relative to lastInput
    /// </summary>
    public CharacterInput WithDeltas(CharacterInput lastInput)
    {
        CharacterInput output = this;

        output.btnJumpPressed = !lastInput.btnJump && btnJump;
        output.btnFirePressed = !lastInput.btnFire && btnFire;
        output.btnSpinPressed = !lastInput.btnSpin && btnSpin;

        output.btnJumpReleased = lastInput.btnJump && !btnJump;
        output.btnFireReleased = lastInput.btnFire && !btnFire;
        output.btnSpinReleased = lastInput.btnSpin && !btnSpin;

        return output;
    }

    public bool Equals(CharacterInput other)
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

    public CharacterInput GenerateLocal()
    {
        return new CharacterInput(); // hmm...
    }
}