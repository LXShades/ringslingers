using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ID 0 is reserved for "null"
/// </summary>
public class AssetLookup : MonoBehaviour
{
    public static AssetLookup singleton { get; private set; }

    [System.Serializable]
    public struct LookupableAsset
    {
        public ushort id;
        public Object asset;
    }

    public LookupableAsset[] assets = new LookupableAsset[0];

    private readonly Dictionary<ushort, Object> assetFromGuid = new Dictionary<ushort, Object>();
    private readonly Dictionary<Object, ushort> guidFromAsset = new Dictionary<Object, ushort>();

    private void Awake()
    {
        Debug.Assert(singleton == null);

        singleton = this;
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        GenerateDictionaries();
    }

    public T GetAsset<T>(ushort id)
        where T : Object
    {
        if (assetFromGuid.TryGetValue(id, out Object returnValue))
        {
            return returnValue as T;
        }
        else
        {
            Log.WriteError($"Cannot find asset {id}");
            return null;
        }
    }

    public ushort GetId<T>(T asset)
        where T : Object
    {
        if (guidFromAsset.TryGetValue(asset, out ushort returnValue))
        {
            return returnValue;
        }
        {
            Log.WriteError($"Cannot find asset \"{asset.name}\"");
            return 0;
        }
    }
    private void GenerateDictionaries()
    {
        assetFromGuid.Clear();
        guidFromAsset.Clear();

        foreach (LookupableAsset asset in assets)
        {
            assetFromGuid.Add(asset.id, asset.asset);
            guidFromAsset.Add(asset.asset, asset.id);
        }
    }

#if UNITY_EDITOR
    public void Populate()
    {
        // Generate ID/Asset pairs
        List<LookupableAsset> newAssetList = new List<LookupableAsset>(1024);

        foreach (var assetGuid in AssetDatabase.FindAssets("t:scriptableobject"))
        {
            ScriptableObject loadedAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(assetGuid));

            if (loadedAsset && loadedAsset as ILookupableAsset != null)
            {
                newAssetList.Add(new LookupableAsset()
                {
                    asset = loadedAsset,
                    id = (ushort)assetGuid.GetHashCode()
                });
            }
        }

        // Resolve potential ID collisions by incrementing ID until collision is gone
        HashSet<ushort> ids = new HashSet<ushort>();
        int numCollisionsResolved = 0;
        for (int i = 0; i < newAssetList.Count; i++)
        {
            int hasCollision = 0;
            while (ids.Contains(newAssetList[i].id) || newAssetList[i].id == 0)
            {
                hasCollision = 1;
                newAssetList[i] = new LookupableAsset()
                {
                    asset = newAssetList[i].asset,
                    id = (ushort)(newAssetList[i].id + 1)
                };
            }

            ids.Add(newAssetList[i].id);
            numCollisionsResolved += hasCollision;
        }

        if (numCollisionsResolved > 0)
        {
            Log.Write($"Resolved {numCollisionsResolved} ID collisions");
        }

        // Save the asset!
        assets = newAssetList.ToArray();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
#endif
}

public interface ILookupableAsset { }