using System.Collections.Generic;
using UnityEngine;

public struct PathFilterError
{
    public bool hasError;
    public string errorInfo;
    public int problemPoint;
}

[System.Serializable]
public class PathFilter
{
    public bool enabled = true;

    public bool hasError => error.hasError;
    public PathFilterError error { get; private set; }

    public void Init() { error = default; }
    public virtual void Apply(List<PathPoint> points) { }
    public virtual void Update(in PathPointTaskParams taskParams, ref CharacterInput input) { }

    protected void OnError(int problemPoint, string reason)
    {
        Debug.LogError($"Path filter {GetType()} error: {reason}");
        error = new PathFilterError() { hasError = true, errorInfo = reason, problemPoint = problemPoint };
    }
}
