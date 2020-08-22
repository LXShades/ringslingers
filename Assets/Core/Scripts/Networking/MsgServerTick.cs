using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


[System.Serializable]
public class MsgTick
{
    // World time before this tick is executed
    public float gameTime;

    // Ticks per-player, if isInGame applies
    public PlayerTick[] playerTicks = new PlayerTick[Netplay.maxPlayers];

    public MemoryStream syncers = new MemoryStream();

    public MsgTick() { }

    public MsgTick(Stream source)
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
                gameTime = reader.ReadSingle();
                syncersLength = reader.ReadInt32();

                // Read inputs
                for (int player = reader.ReadByte(); player != 255 && player != -1; player = reader.ReadByte())
                {
                    playerTicks[player].FromStream(reader);
                }
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
        catch
        {
            Debug.LogError("Could not read server tick");
        }
    }

    public void ToStream(Stream stream)
    {
        // Write key info
        using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
        {
            writer.Write(gameTime);
            writer.Write((int)syncers.Length);

            // Write players
            for (int i = 0; i < playerTicks.Length; i++)
            {
                if (playerTicks[i].isInGame)
                {
                    writer.Write((byte)i);
                    playerTicks[i].ToStream(writer);
                }
            }
            writer.Write((byte)255);
        }

        // Add syncers to the message
        syncers.WriteTo(stream);
    }
}