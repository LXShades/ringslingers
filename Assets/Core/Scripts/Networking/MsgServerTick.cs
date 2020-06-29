using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MsgServerTick
{
    // Frame time before this tick is executed
    public float time;

    // DeltaTime the server will tick from here
    public float deltaTime;

    // PlayerInputs during execution of this tick
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
        try
        {
            // Read key info
            int syncersLength = 0;
            using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true))
            {
                time = reader.ReadSingle();
                deltaTime = reader.ReadSingle();
                syncersLength = reader.ReadInt32();
            }

            // Read inputs
            for (int player = stream.ReadByte(); player != 255 && player != -1; player = stream.ReadByte())
            {
                isPlayerInGame[player] = true;
                playerInputs[player].FromStream(stream);
            }

            // Read syncers
            syncers = new MemoryStream();

            if (syncersLength > 0)
            {
                byte[] becauseCopyToJustDoesntWork = new byte[syncersLength];

                stream.Read(becauseCopyToJustDoesntWork, 0, syncersLength);
                syncers.Write(becauseCopyToJustDoesntWork, 0, syncersLength);
            }
        }
        catch {
            Debug.LogError("Could not read server tick");
        }
    }

    public void ToStream(Stream stream)
    {
        // Write key info
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(time);
            writer.Write(deltaTime);
            writer.Write((int)syncers.Length);
        }

        // Write inputs
        for (int i = 0; i < playerInputs.Length; i++)
        {
            if (isPlayerInGame[i])
            {
                stream.WriteByte((byte)i);
                playerInputs[i].ToStream(stream);
            }
        }
        stream.WriteByte(255);

        // Add syncers to the message
        syncers.WriteTo(stream);
    }
}