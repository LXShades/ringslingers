using UnityEngine;

public static class Singleton
{
    public static T GetOrCreate<T>(ref T val, string name = "Singleton") where T : Component
    {
        if (val == null)
        {
            GameObject obj = new GameObject(name);

            val = obj.AddComponent<T>();
        }

        return val;
    }
}