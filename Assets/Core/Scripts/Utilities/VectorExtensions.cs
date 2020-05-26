using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VectorExtensions
{
    public static Vector3 Horizontal(this Vector3 vec)
    {
        return new Vector3(vec.x, 0, vec.z);
    }

    public static void SetHorizontal(ref this Vector3 vec, Vector3 value)
    {
        vec.x = value.x;
        vec.z = value.z;
    }
}