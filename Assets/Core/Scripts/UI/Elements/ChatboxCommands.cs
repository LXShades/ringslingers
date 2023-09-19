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
    private Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();

    private void Awake()
    {
        RegisterCommand("help", ConsoleCommand_Help);
        RegisterCommand("endround", Netplay.singleton.ConsoleCommand_EndRound);
        RegisterCommand("addbot", Netplay.singleton.ConsoleCommand_AddBot);
        RegisterCommand("addfollowbot", Netplay.singleton.ConsoleCommand_AddFollowBot);
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

        commands[command].Invoke(parametersAsText.ToArray());
        return true;
    }

    private void RegisterCommand(string name, Action function)
    {
        commands.Add(name, parameters => function());
    }

    private void RegisterCommand<TA>(string name, Action<TA> function)
    {
        commands.Add(name, parameters =>
        {
            if (TryParseParameter<TA>(parameters[1], out TA taValue, out string error))
                function(taValue);
            else
                MessageFeed.PostLocal(error);
        });
    }

    private bool TryParseParameter<T>(string argument, out T parsedValue, out string error)
    {
        error = null;
        switch ((T)default)
        {
            case string _:
            {
                parsedValue = (T)(object)argument;
                return true;
            }
            case int _:
            {
                bool canParse = int.TryParse(argument, out int parsedInt);
                parsedValue = (T)(object)parsedInt;
                if (!canParse)
                    error = $"{argument} is not a whole number";
                return canParse;
            }
            case float _:
            {
                bool canParse = float.TryParse(argument, out float parsedFloat);
                parsedValue = (T)(object)parsedFloat;
                if (!canParse)
                    error = $"{argument} is not a number";
                return canParse;
            }
            case bool _:
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
            default:
                error = "Can't parse argument of type {typeof(T)}: type not supported";
                parsedValue = (T)default;
                return false;
        }
    }

    private void ConsoleCommand_Help()
    {
        StringBuilder sb = new StringBuilder();

        foreach (KeyValuePair<string, Action<string[]>> command in commands)
            sb.AppendLine(command.Key);

        MessageFeed.PostLocal(sb.ToString());
    }
}
