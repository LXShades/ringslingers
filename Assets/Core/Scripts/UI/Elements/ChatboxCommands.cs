using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Handles slash commands typed into the chatbox
/// </summary>
public class ChatboxCommands : MonoBehaviour
{
    public struct Command
    {
        public Action<string[]> command;
        public string description;
        public bool isAdminCommand;
    }

    private Dictionary<string, Command> commands = new Dictionary<string, Command>();

    private void Awake()
    {
        // Misc commands
        RegisterCommand("help", "Shows all commands you can use", ConsoleCommand_Help, false);
        RegisterCommand<string>("admin", "<password> Tries to log you in as an admin", Netplay.singleton.ConsoleCommand_Admin, false);
        
        // Admin commands
        RegisterCommand("endround", "Ends the current round or intermission screen", Netplay.singleton.ConsoleCommand_EndRound, true);
        RegisterCommand("addbot", "Adds a basic bot (testing)", Netplay.singleton.ConsoleCommand_AddBot, true);
        RegisterCommand("addfollowbot", "Adds a bot that follows you (works on host only)", Netplay.singleton.ConsoleCommand_AddFollowBot, true);
    }

    public bool OnCommandSubmitted(string commandAndParameters, out string error)
    {
        error = "";

        // check if it's a valid command
        List<string> parametersAsText = new List<string>(commandAndParameters.Split(" "));
        string command = parametersAsText[0].ToLower();

        if (parametersAsText.Count == 0 || !commands.ContainsKey(command))
        {
            error = parametersAsText.Count > 0 ? $"Invalid command '{parametersAsText[0]}'" : "No command specified";
            return false;
        }

        // join spaces in "quoted" blocks
        for (int i = 0; i < parametersAsText.Count; i++)
        {
            if (parametersAsText[i].StartsWith("\""))
            {
                while (!parametersAsText[i].EndsWith("\""))
                {
                    if (i + 1 < parametersAsText.Count)
                    {
                        parametersAsText[i] = parametersAsText[i] + parametersAsText[i + 1];
                        parametersAsText.RemoveAt(i + 1);
                    }
                    else
                    {
                        error = $"Parameter {parametersAsText[i]}: double quoted parameter is not closed correctly";
                        return false;
                    }
                }
            }
        }

        // run the command
        if (commands[command].isAdminCommand && !NetworkServer.active)
        {
            // try and run it remotely
            if (GameState.Get(out GameState_ServerSettings serverSettings))
                serverSettings.CmdRunAdminCommand(commandAndParameters);
            return true;
        }
        else
        {
            commands[command].command.Invoke(parametersAsText.ToArray());
            return true;
        }
    }

    private void RegisterCommand(string name, string description, Action function, bool isAdminCommand)
    {
        commands.Add(name, new Command()
        {
            command = parameters => function(),
            description = description,
            isAdminCommand = isAdminCommand
        });
    }

    private void RegisterCommand<TA>(string name, string description, Action<TA> function, bool isAdminCommand)
    {
        commands.Add(name, new Command()
        {
            command = parameters =>
            {
                if (TryParseParameter<TA>(parameters[1], out TA taValue, out string error))
                    function(taValue);
                else
                    MessageFeed.PostLocal(error);
            },
            description = description,
            isAdminCommand = isAdminCommand
        });
    }

    private bool TryParseParameter<T>(string argument, out T parsedValue, out string error)
    {
        error = null;

        if (typeof(T) == typeof(string))
        {
            parsedValue = (T)(object)argument;
            return true;
        }
        else if (typeof(T) == typeof(int))
        {
            bool canParse = int.TryParse(argument, out int parsedInt);
            parsedValue = (T)(object)parsedInt;
            if (!canParse)
                error = $"{argument} is not a whole number";
            return canParse;
        }
        else if (typeof(T) == typeof(float))
        {
            bool canParse = float.TryParse(argument, out float parsedFloat);
            parsedValue = (T)(object)parsedFloat;
            if (!canParse)
                error = $"{argument} is not a number";
            return canParse;
        }
        else if (typeof(T) == typeof(bool))
        {
            bool isYes = argument == "1" || string.Compare(argument, "yes", true) == 0 || string.Compare(argument, "on", true) == 0;
            bool isNo = argument == "0" || string.Compare(argument, "no", true) == 0 || string.Compare(argument, "false", true) == 0;
            if (isYes || isNo)
            {
                parsedValue = (T)(object)isYes;
                return true;
            }
            else
            {
                error = $"{argument} is not a valid boolean (yes/no, on/off, etc) value";
                parsedValue = default;
                return false;
            }
        }
        else
        {
            error = $"Can't parse argument of type {typeof(T)}: type not supported";
            parsedValue = (T)default;
            return false;
        }
    }

    private void ConsoleCommand_Help()
    {
        StringBuilder sb = new StringBuilder();

        foreach (KeyValuePair<string, Command> command in commands)
            sb.AppendLine(command.Key);

        MessageFeed.PostLocal(sb.ToString());
    }
}
