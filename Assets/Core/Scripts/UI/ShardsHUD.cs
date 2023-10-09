using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ShardsHUD : MonoBehaviour
{
    public GameObject map;

    private void Update()
    {
        bool shouldMapBeVisible = MatchState.Get(out MatchShards shardsState);
        if (shouldMapBeVisible != map.activeSelf)
            map.SetActive(shouldMapBeVisible);
    }
}
