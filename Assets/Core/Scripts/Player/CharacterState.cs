using Mirror;
using System;
using UnityEngine;

[Serializable]
public struct CharacterState : IEquatable<CharacterState>
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 up;
    public CharacterMovement.State state;

    public void Serialize(NetworkWriter writer)
    {
        // 25 bytes
        writer.WriteVector3(position);
        writer.WriteInt32(Compressor.CompressQuaternion(rotation));
        writer.WriteUInt16(Compressor.CompressFloat16(velocity.x, -100f, 100f));
        writer.WriteUInt16(Compressor.CompressFloat16(velocity.y, -100f, 100f));
        writer.WriteUInt16(Compressor.CompressFloat16(velocity.z, -100f, 100f));
        writer.WriteInt16(Compressor.CompressNormal16(up));
        writer.Write((byte)state);
    }

    public void Deserialize(NetworkReader reader)
    {
        position = reader.ReadVector3();
        rotation = Compressor.DecompressQuaternion(reader.ReadInt32());
        velocity = new Vector3(Compressor.DecompressFloat16(reader.ReadUInt16(), -100f, 100f), Compressor.DecompressFloat16(reader.ReadUInt16(), -100f, 100f), Compressor.DecompressFloat16(reader.ReadUInt16(), -100f, 100f));
        up = Compressor.DecompressNormal16(reader.ReadInt16());
        state = (CharacterMovement.State)reader.ReadByte();
    }

    public void DebugDraw(Color colour)
    {
        DebugExtension.DebugCapsule(position, position + up, colour, 0.25f);
        DebugExtension.DebugArrow(position + up * 0.5f, velocity * 0.1f, colour);
        DebugExtension.DebugCapsule(position, position + rotation * Vector3.up, colour, 0.25f * 0.975f);
    }

    public bool Equals(CharacterState other)
    {
        return other.position == position && other.rotation == rotation && other.velocity == velocity && other.state == state && up == other.up;
    }

    public override string ToString()
    {
        return $"Pos: {position.ToString()}\nRot: {rotation.ToString()}\nVel: {velocity.ToString()}\nUp: {up.ToString()}\nState: {state}";
    }
}

public static class MoveStateReaderWriter
{
    public static void WriteMoveState(this NetworkWriter writer, CharacterState state)
    {
        state.Serialize(writer);
    }

    public static CharacterState ReadMoveState(this NetworkReader reader)
    {
        CharacterState state = default;
        state.Deserialize(reader);
        return state;
    }
}