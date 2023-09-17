using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameState_Map : GameStateComponent
{
    public MapConfiguration activeMap
    {
        get => _activeMap;
        set
        {
            _activeMap = value;

            if (activeMapRotation == null || !activeMapRotation.maps.Contains(_activeMap))
            {
                // we need to make sure we have a valid map rotation (todo flimsy code here)
                foreach (MapRotation mapRotation in RingslingersContent.loaded.mapRotations)
                {
                    if (mapRotation.maps.Contains(_activeMap))
                        activeMapRotation = mapRotation;
                }
            }

            if (activeMapRotation == null)
                Debug.LogError($"Active map rotation could not be found from this map, this will break clients' knowledge of the current map. Likely an invalid activeMap (such as one from outside RingslingersContent.loaded) was set.");

            activeMapIndex = activeMapRotation != null ? activeMapRotation.maps.IndexOf(_activeMap) : -1;
            activeMapRotationIndex = RingslingersContent.loaded.mapRotations.IndexOf(activeMapRotation);

            if (!NetworkClient.active)
                onMapChanged?.Invoke(activeMap);
        }
    }
    private MapConfiguration _activeMap;
    public MapRotation activeMapRotation { get; private set; }

    // Sent from server to client so the client know what level they're in
    [SyncVar(hook = nameof(OnActiveMapIndexOrRotationIndexChanged))]
    private int activeMapRotationIndex;
    [SyncVar(hook = nameof(OnActiveMapIndexOrRotationIndexChanged))]
    private int activeMapIndex;

    // Called on server immediately when map is known, called on client once the syncvar gets updated
    public Action<MapConfiguration> onMapChanged;

    public void ServerLoadMap(MapConfiguration map)
    {
        activeMap = map;
        NetMan.singleton.ServerChangeScene(map.path, true);
    }

    public void ServerNextMap()
    {
        if (!NetworkServer.active)
        {
            Log.WriteWarning("[ServerNextMap()] Only the server can change the map");
            return;
        }

        if (NetworkServer.isLoadingScene)
        {
            Log.WriteWarning($"[ServerNextMap()] The map is already changing");
            return;
        }

        if (RingslingersContent.loaded.mapRotations.Count == 0)
        {
            Log.WriteWarning("[ServerNextMap()] There are no map rotations loaded");
            return;
        }

        List<MapConfiguration> maps = activeMapRotation?.maps ?? RingslingersContent.loaded.mapRotations[0].maps;

        if (maps == null || maps.Count == 0)
        {
            Log.WriteError("Cannot load levels database: list is empty or null");
            return;
        }

        // Move to the next map
        int initialMapIndex = maps.IndexOf(activeMap);
        int nextMapIndex;
        for (nextMapIndex = (initialMapIndex + 1) % maps.Count; nextMapIndex != initialMapIndex; nextMapIndex = (nextMapIndex + 1) % maps.Count)
        {
            // todo: check player count
            if (!maps[nextMapIndex].isDevOnly || Application.isEditor)
                break;
        }

        ServerLoadMap(maps[nextMapIndex]);
    }

    private void OnActiveMapIndexOrRotationIndexChanged(int oldValue, int newValue)
    {
        if (activeMapRotationIndex != -1 && activeMapIndex != -1)
        {
            activeMapRotation = RingslingersContent.loaded.mapRotations[activeMapRotationIndex];
            activeMap = activeMapRotation.maps[activeMapIndex];

            onMapChanged?.Invoke(activeMap);
        }
    }
}
