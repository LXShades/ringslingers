using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MsgClientTick
{
    public float localTime;
    public float serverTime;

    public InputCmds playerInputs;

    public Vector3 localPosition; // position of the local player, used for adjustments

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
            localPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
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
            writer.Write(localPosition.x);
            writer.Write(localPosition.y);
            writer.Write(localPosition.z);
        }

        // Write inputs
        playerInputs.ToStream(stream);
    }
}