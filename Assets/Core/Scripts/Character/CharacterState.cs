using System;
using UnityEngine;

[Serializable]
public struct CharacterState : ITickerState<CharacterState>
{
    private const float kVelocityRange = 100f;

    // external data - these are compressed
    public Vector3 position
    {
        get => _position;
        set => _position = value;
    }
    public Quaternion rotation
    {
        get => Compressor.DecompressQuaternion32(_rotation);
        set => _rotation = Compressor.CompressQuaternion32(value);
    }
    public Vector3 velocity
    {
        get => new Vector3(Compressor.DecompressFloat16(_velocityX, -kVelocityRange, kVelocityRange), Compressor.DecompressFloat16(_velocityY, -kVelocityRange, kVelocityRange), Compressor.DecompressFloat16(_velocityZ, -kVelocityRange, kVelocityRange));
        set
        {
            _velocityX = Compressor.CompressFloat16(value.x, -kVelocityRange, kVelocityRange);
            _velocityY = Compressor.CompressFloat16(value.y, -kVelocityRange, kVelocityRange);
            _velocityZ = Compressor.CompressFloat16(value.z, -kVelocityRange, kVelocityRange);
        }
    }
    public Vector3 up
    {
        get => Compressor.DecompressNormal24(_upLow | ((uint)_upHigh << 16));
        set
        {
            uint compressed = Compressor.CompressNormal24(value);
            _upLow = (ushort)(compressed & 0xFFFF);
            _upHigh = (byte)(compressed >> 16);
        }
    }
    public CharacterMovement.State state
    {
        get => (CharacterMovement.State)_state;
        set => _state = (byte)value;
    }
    public float spindashChargeLevel
    {
        get => _spindashChargeAmount / 65535f;
        set => _spindashChargeAmount = (ushort)(value * 65535f);
    }

    // internal - actual data sent/received and confirmed/deconfirmed
    // public because Mirror only serializes public stuff
    // 26 bytes
    public Vector3 _position;
    public uint _rotation;
    public ushort _velocityX;
    public ushort _velocityY;
    public ushort _velocityZ;
    public ushort _upLow;
    public byte _upHigh;
    public byte _state;
    public ushort _spindashChargeAmount;

    public void DebugDraw(Color colour)
    {
        DebugExtension.DebugCapsule(position, position + up, colour, 0.25f);
        DebugExtension.DebugArrow(position + up * 0.5f, velocity * 0.1f, colour);
        DebugExtension.DebugCapsule(position, position + rotation * Vector3.up, colour, 0.25f * 0.975f);
    }

    public bool Equals(CharacterState other)
    {
        return other._position == _position
            && other._rotation == _rotation
            && other._velocityX == _velocityX && other._velocityY == _velocityY && other._velocityZ == _velocityZ
            && other._upLow == _upLow
            && other._upHigh == _upHigh
            && other._state == _state
            && other._spindashChargeAmount == _spindashChargeAmount;
    }

    // Compresses upIn in the same way as regular up and returns the result. Used to quantize in the same way we expect it to be quantized when saved/loaded
    public static Vector3 RecompressUp(Vector3 upIn)
    {
        return Compressor.DecompressNormal24(Compressor.CompressNormal24(upIn));
    }

    public override string ToString()
    {
        return $"Pos: {position.ToString()}\nRot: {rotation.ToString()}\nVel: {velocity.ToString()}\nUp: {up.ToString()}\nState: {state}";
    }

    public static string LogDifferences(CharacterState stateA, CharacterState stateB)
    {
        string differences = "";

        if (stateA.position != stateB.position)
            differences += $"A-pos: {stateA.position} B-pos: {stateB.position}\n";
        if (stateA.rotation != stateB.rotation)
            differences += $"A-rot: {stateA.rotation} B-rot: {stateB.rotation}\n";
        if (stateA.velocity != stateB.velocity)
            differences += $"A-vel: {stateA.velocity} B-vel: {stateB.velocity}\n";
        if (stateA.up != stateB.up)
            differences += $"A-up: {stateA.up} B-up: {stateB.up}\n";
        if (stateA.spindashChargeLevel != stateB.spindashChargeLevel)
            differences += $"A-dash: {stateA.spindashChargeLevel.ToString("F2")} B-dash: {stateB.spindashChargeLevel.ToString("F2")}";
        return differences;
    }
}