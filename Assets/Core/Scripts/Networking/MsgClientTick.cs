using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MsgClientTick
{
    public float serverTime;

    public PlayerTick tick;

    public MsgClientTick() { }

    public MsgClientTick(Stream source)
    {
        FromStream(source);
    }

    public void FromStream(Stream stream)
    {
        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true))
        {
            serverTime = reader.ReadSingle();
            tick.FromStream(reader);
        }
    }

    public void ToStream(Stream stream)
    {
        // Write key info
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(serverTime);
            tick.ToStream(writer);
        }
    }
}