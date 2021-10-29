using System;
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

    public enum ThingIdType
    {
        Single = 0,
        Range = 1
    }

    [Flags]
    public enum ThingFlags
    {
        Flag1 = 1,
        Flag2 = 2,
        Flag4 = 4,
        Flag8 = 8
    }

    [System.Serializable]
    public struct IdPrefabPair
    {
        public ThingIdType idType;
        public int thingId;
        public int thingIdEnd;
        public ThingFlags requiredFlags;
        public GameObject prefab;
    }

    public List<IdPrefabPair> idPrefabPairs = new List<IdPrefabPair>();

    public void ReadThings(Thing[] things)
    {
        int thingNum = 0;
        foreach (Thing thing in things)
        {
            GameObject prefab = idPrefabPairs.Find(a =>
                ((a.idType == ThingIdType.Single && a.thingId == thing.type) || (a.idType == ThingIdType.Range && thing.type >= a.thingId && thing.type <= a.thingIdEnd))
                && (thing.flags & (int)a.requiredFlags) == (int)a.requiredFlags
            ).prefab;

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
                obj.transform.rotation = Quaternion.Euler(0f, 90f - thing.angle, 0f);
                obj.transform.SetParent(transform, true);
            }

            ++thingNum;
        }
    }
}
