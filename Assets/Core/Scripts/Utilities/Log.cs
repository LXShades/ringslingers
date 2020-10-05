using System;
using UnityEngine;

public static class Log
{
    public static void Write(string logText, LogType type = LogType.Log)
    {
        System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
        System.Diagnostics.StackFrame frame = trace.GetFrame(1);
        string filename = frame.GetFileName();
        
        logText = $"({filename.Substring(Math.Max(filename.LastIndexOf("/"), filename.LastIndexOf("\\")) + 1)}:{frame.GetFileLineNumber()}) [{frame.GetMethod().DeclaringType}.{frame.GetMethod().Name}] {logText}";

        switch (type)
        {
            case LogType.Log: Debug.Log(logText); break;
            case LogType.Warning: Debug.LogWarning(logText); break;
            case LogType.Error: Debug.LogError(logText); break;
        }
    }
    public static void WriteWarning(string logText)
    {
        Write(logText, LogType.Warning);
    }

    public static void WriteError(string logText)
    {
        Write(logText, LogType.Error);
    }
}