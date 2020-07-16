using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

[System.Serializable]
public class MsgTick
{
    // Frame time before this tick is executed
    public float time;

    // DeltaTime the server will tick from here
    public float deltaTime;

    // Local time of the receiving player, as currently known to the server, as of the last tick
    public float localTime;

    // Position of all players at this time (prior to the tick), as currently known to the server
    public Vector3[] playerPositions = new Vector3[Netplay.maxPlayers];

    // PlayerInputs during execution of this tick
    public InputCmds[] playerInputs = new InputCmds[Netplay.maxPlayers];
    public bool[] isPlayerInGame = new bool[Netplay.maxPlayers];

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
                time = reader.ReadSingle();
                deltaTime = reader.ReadSingle();
                localTime = reader.ReadSingle();
                syncersLength = reader.ReadInt32();

                // Read inputs
                for (int player = reader.ReadByte(); player != 255 && player != -1; player = reader.ReadByte())
                {
                    isPlayerInGame[player] = true;
                    playerInputs[player].FromStream(stream);
                    playerPositions[player] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
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
            writer.Write(localTime);
            writer.Write((int)syncers.Length);

            // Write players
            for (int i = 0; i < playerInputs.Length; i++)
            {
                if (isPlayerInGame[i])
                {
                    writer.Write((byte)i);
                    playerInputs[i].ToStream(stream);
                    writer.Write(playerPositions[i].x);
                    writer.Write(playerPositions[i].y);
                    writer.Write(playerPositions[i].z);
                }
            }
            writer.Write((byte)255);
        }

        // Add syncers to the message
        syncers.WriteTo(stream);
    }
}