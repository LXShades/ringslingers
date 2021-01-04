using UnityEngine;

public static class VectorExtensions
{
    /// <summary>
    /// Returns the horizontal components (x,z) of the vector
    /// </summary>
    public static Vector3 Horizontal(this Vector3 vec)
    {
        return new Vector3(vec.x, 0, vec.z);
    }

    /// <summary>
    /// Sets the horizontal component of the vector only
    /// </summary>
    public static void SetHorizontal(ref this Vector3 vec, Vector3 value)
    {
        vec.x = value.x;
        vec.z = value.z;
    }

    /// <summary>
    /// Returns how far along the axis the vector goes. The axis does not need to be normalized
    /// </summary>
    public static float GetAlongAxis(ref this Vector3 vec, Vector3 axis)
    {
        float mag = axis.sqrMagnitude;
        if (mag <= 0.999f || mag >= 1.001f)
            axis.Normalize();

        return Vector3.Dot(vec, axis);
    }

    /// <summary>
    /// Sets how far along the axis the factor goes. The axis does not need to be normalized
    /// </summary>
    public static void SetAlongAxis(ref this Vector3 vec, Vector3 axis, float magnitude)
    {
        float mag = axis.sqrMagnitude;
        if (mag <= 0.999f || mag >= 1.001f)
            axis.Normalize();

        vec = vec + axis * (magnitude - Vector3.Dot(vec, axis));
    }

    /// <summary>
    /// Sets the component of the vector going along the plane with a normal of planeNormal, ignoring components that go along the planeNormal itself
    /// For example, if planeNormal is Vector3.up, this is the same as SetHorizontal.
    /// </summary>
    public static void SetAlongPlane(ref this Vector3 vec, Vector3 planeNormal, Vector3 valueAlongPlane)
    {
        float mag = planeNormal.sqrMagnitude;
        if (mag <= 0.999f || mag >= 1.001f)
            planeNormal.Normalize();

        vec = valueAlongPlane + planeNormal * (Vector3.Dot(vec, planeNormal) - Vector3.Dot(valueAlongPlane, planeNormal));
    }

    /// <summary>
    /// Gets the component of the vector going along the plane with a normal of planeNormal, ignoring components that go along the planeNormal itself.
    /// For example, if planeNormal is Vector3.up, this is the same as Horizontal.
    /// </summary>
    /// <param name="vec"></param>
    /// <param name="planeNormal"></param>
    /// <returns></returns>
    public static Vector3 AlongPlane(ref this Vector3 vec, Vector3 planeNormal)
    {
        float mag = planeNormal.sqrMagnitude;
        if (mag <= 0.999f || mag >= 1.001f)
            planeNormal.Normalize();

        return vec - planeNormal * Vector3.Dot(vec, planeNormal);
    }
}