using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public struct PlayerTick
{
    public bool isInGame;

    public Vector3 position;
    public Vector3 velocity;
    public float localTime;
    public CharacterMovement.State state;

    public PlayerInput input;

    public void FromStream(BinaryReader reader)
    {
        isInGame = true;
        input.FromStream(reader.BaseStream);
        position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        velocity = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        state = (CharacterMovement.State)reader.ReadByte();
        localTime = reader.ReadSingle();
    }

    public void ToStream(BinaryWriter writer)
    {
        input.ToStream(writer.BaseStream);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(velocity.x);
        writer.Write(velocity.y);
        writer.Write(velocity.z);
        writer.Write((byte)state);
        writer.Write(localTime);
    }
}