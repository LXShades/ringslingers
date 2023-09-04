using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSelector : MonoBehaviour
{
    public Dropdown mapRotationsDropdown;
    public Transform mapSelector;

    public MapButton mapButtonPrefab;

    private List<MapButton> mapButtons = new List<MapButton>();

    private MapConfiguration selectedMap = null;

    private void OnEnable()
    {
        // Refill the map rotations dropdown
        mapRotationsDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentMapRotationIndex = RingslingersContent.loaded.mapRotations.IndexOf(GameManager.singleton.activeMapRotation);
        foreach (MapRotation mapRotation in RingslingersContent.loaded.mapRotations)
            options.Add(mapRotation.name);
        
        mapRotationsDropdown.AddOptions(options);
        mapRotationsDropdown.SetValueWithoutNotify(currentMapRotationIndex);

        mapRotationsDropdown.onValueChanged.AddListener(OnMapRotationSelected);

        // Refresh the map selector
        RefreshMapSelector(mapRotationsDropdown.value);
    }

    private void OnDisable()
    {
        mapRotationsDropdown.onValueChanged.RemoveListener(OnMapRotationSelected);
    }

    private void RefreshMapSelector(int mapRotationIndex)
    {
        selectedMap = null;

        // Cleanup original buttons
        foreach (var mapButton in mapButtons)
        {
            if (mapButton != null)
                Destroy(mapButton.gameObject);
        }
        mapButtons.Clear();

        // Instantiate new buttons
        if (mapRotationIndex >= 0 && mapRotationIndex < RingslingersContent.loaded.mapRotations.Count)
        {
            foreach (MapConfiguration map in RingslingersContent.loaded.mapRotations[mapRotationIndex].maps)
            {
                if (map.isDevOnly && !Application.isEditor)
                    continue;

                MapButton mapButtonInstance = Instantiate(mapButtonPrefab, mapSelector);
                mapButtons.Add(mapButtonInstance);
                mapButtonInstance.SetInfo($"{map.friendlyName}\n{map.defaultGameModePrefab.name}", map.screenshot);
                mapButtonInstance.GetComponent<Button>().onClick.AddListener(() => OnLevelButtonPressed(map));
            }
        }
    }

    private void OnLevelButtonPressed(MapConfiguration map) => selectedMap = map;

    public void OnGoButtonPressed()
    {
        if (selectedMap != null)
        {
            if (NetworkServer.active)
                Netplay.singleton.ServerLoadLevel(selectedMap);
            else if (!NetworkServer.active && !NetworkClient.active) // this might have been called from the main menu?
                Netplay.singleton.HostServer(selectedMap);
            else
                Debug.LogError("Clients can not change the game map!");
        }
    }

    private void OnMapRotationSelected(int selectedIndex)
    {
        RefreshMapSelector(selectedIndex);
    }
}
