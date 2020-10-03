using System;
using UnityEngine;

public static class Log
{
    public static void Write(string logText)
    {
        System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
        System.Diagnostics.StackFrame frame = trace.GetFrame(1);
        string filename = frame.GetFileName();

        Debug.Log($"({filename.Substring(Math.Max(filename.LastIndexOf("/"), filename.LastIndexOf("\\")) + 1)}:{frame.GetFileLineNumber()}) [{frame.GetMethod().DeclaringType}.{frame.GetMethod().Name}] {logText}");
    }

    public static void WriteError(string logText)
    {
        System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(true);
        System.Diagnostics.StackFrame frame = trace.GetFrame(1);
        string filename = frame.GetFileName();

        Debug.LogError($"({filename.Substring(Math.Max(filename.LastIndexOf("/"), filename.LastIndexOf("\\")) + 1)}:{frame.GetFileLineNumber()}) [{frame.GetMethod().DeclaringType}.{frame.GetMethod().Name}] {logText}");
    }
}