using System;
using UnityEngine;

public static class Log
{
    public static void Write(string logText, LogType type = LogType.Log, int frameSteps = 1)
    {
        System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
        System.Diagnostics.StackFrame frame = trace.GetFrame(frameSteps);
        string filename = frame.GetFileName();
        
        logText = $"[{frame.GetMethod().DeclaringType}.{frame.GetMethod().Name}] {logText} ({filename.Substring(Math.Max(filename.LastIndexOf("/"), filename.LastIndexOf("\\")) + 1)}:{frame.GetFileLineNumber()})";

        switch (type)
        {
            case LogType.Log: Debug.Log(logText); break;
            case LogType.Warning: Debug.LogWarning(logText); break;
            case LogType.Error: Debug.LogError(logText); break;
        }
    }
    public static void WriteWarning(string logText)
    {
        Write(logText, LogType.Warning, 2);
    }

    public static void WriteError(string logText)
    {
        Write(logText, LogType.Error, 2);
    }

    public static void WriteException(Exception e)
    {
        Debug.LogException(e);
    }
}