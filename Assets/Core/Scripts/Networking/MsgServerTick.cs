using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MsgServerTick
{
    public float time;
    public float deltaTime;

    public InputCmds[] playerInputs = new InputCmds[Netplay.maxPlayers];
    public bool[] isPlayerInGame = new bool[Netplay.maxPlayers];

    public MemoryStream syncers = new MemoryStream();

    public MsgServerTick() { }

    public MsgServerTick(Stream source)
    {
        FromStream(source);
    }

    public void FromStream(Stream stream)
    {
        int syncersLength = 0;
        using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true))
        {
            time = reader.ReadSingle();
            deltaTime = reader.ReadSingle();
            syncersLength = reader.ReadInt32();
        }

        playerInputs = Netplay.singleton.DeserializePlayerInputs(stream, isPlayerInGame);

        syncers = new MemoryStream();

        if (syncersLength > 0)
        {
            byte[] becauseCopyToJustDoesntWork = new byte[syncersLength];

            stream.Read(becauseCopyToJustDoesntWork, 0, syncersLength);
            syncers.Write(becauseCopyToJustDoesntWork, 0, syncersLength);
        }
    }

    public void ToStream(Stream stream)
    {
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(time);
            writer.Write(deltaTime);
            writer.Write((int)syncers.Length);
        }

        Netplay.singleton.SerializePlayerInputs(stream);

        // Add syncers to the message
        stream.WriteByte(255);

        syncers.WriteTo(stream);
    }
}