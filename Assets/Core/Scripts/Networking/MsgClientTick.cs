using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MsgClientTick
{
    public float localTime;
    public float serverTime;
    public float deltaTime;

    public InputCmds playerInputs;

    public MsgClientTick() { }

    public MsgClientTick(Stream source)
    {
        FromStream(source);
    }

    public void FromStream(Stream stream)
    {
        // Read key info
        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true))
        {
            localTime = reader.ReadSingle();
            serverTime = reader.ReadSingle();
            deltaTime = reader.ReadSingle();
        }

        // Read inputs
        playerInputs.FromStream(stream);
    }

    public void ToStream(Stream stream)
    {
        // Write key info
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(localTime);
            writer.Write(serverTime);
            writer.Write(deltaTime);
        }

        // Write inputs
        playerInputs.ToStream(stream);
    }
}