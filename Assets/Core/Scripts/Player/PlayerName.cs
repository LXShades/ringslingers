using TMPro;
using UnityEngine;

public class PlayerName : MonoBehaviour
{
    public Character player;
    public TextMeshPro text;

    private string lastPlayerName = "";

    void LateUpdate()
    {
        if (lastPlayerName != player.playerName)
        {
            lastPlayerName = player.playerName;
            text.text = player.playerName;
        }

        if (text.enabled != (player != Netplay.singleton.localPlayer))
            text.enabled = player != Netplay.singleton.localPlayer;

        transform.rotation = Quaternion.LookRotation(-(GameManager.singleton.camera.transform.position - transform.position)); // why is it negative? i don't know.
    }

    private void OnValidate()
    {
        if (player == null)
            player = GetComponentInParent<Character>();
        if (text == null)
            text = GetComponent<TextMeshPro>();
    }
}
