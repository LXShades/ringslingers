using System;
using UnityEngine;

[Serializable]
public struct CharacterState : IEquatable<CharacterState>
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
        get => Compressor.DecompressQuaternion(_rotation);
        set => _rotation = Compressor.CompressQuaternion(value);
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
        get => Compressor.DecompressNormal16(_up);
        set => _up = Compressor.CompressNormal16(value);
    }
    public CharacterMovement.State state
    {
        get => (CharacterMovement.State)_state;
        set => _state = (byte)value;
    }

    // internal - actual data sent/received and confirmed/deconfirmed
    // public because Mirror only serializes public stuff
    // 25 bytes
    public Vector3 _position;
    public int _rotation;
    public ushort _velocityX;
    public ushort _velocityY;
    public ushort _velocityZ;
    public short _up;
    public byte _state;

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
            && other._up == _up
            && other._state == _state;
    }

    public override string ToString()
    {
        return $"Pos: {position.ToString()}\nRot: {rotation.ToString()}\nVel: {velocity.ToString()}\nUp: {up.ToString()}\nState: {state}";
    }
}