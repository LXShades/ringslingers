using System.Collections.Generic;
using UnityEngine;

public class JsonThingImporter : MonoBehaviour
{
    [System.Serializable]
    public class Thing
    {
        public int x;
        public int y;
        public int angle;
        public int type;
        public int flags;

        public bool skill2;
        public bool skill3;
        public bool skill4;
        public bool ambush;
        public bool multiplayer;

        public int height;
        public int settings;
        public int floorHeight;
        public int ceilingHeight;
    }

    [System.Serializable]
    public class ExtractedThings
    {
        public Thing[] things;
    }

    [System.Serializable]
    public struct IdPrefabPair
    {
        public int thingId;
        public GameObject prefab;
    }

    public List<IdPrefabPair> idPrefabPairs = new List<IdPrefabPair>();

    public void ReadThings(Thing[] things)
    {
        int thingNum = 0;
        foreach (Thing thing in things)
        {
            GameObject prefab = idPrefabPairs.Find(a => a.thingId == thing.type).prefab;

            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab);
                // Relink it to its prefab
#if UNITY_EDITOR
#pragma warning disable 618, 612
                obj = UnityEditor.PrefabUtility.ConnectGameObjectToPrefab(obj, prefab);
#pragma warning restore 618, 612
#endif

                obj.name = $"{prefab.name} (thing {thingNum})";
                obj.transform.position = new Vector3(thing.x / 64f, (thing.floorHeight + thing.height) / 64f, thing.y / 64f);
                obj.transform.rotation = Quaternion.Euler(0f, thing.angle, 0f);
                obj.transform.SetParent(transform, true);
            }

            ++thingNum;
        }
    }
}
