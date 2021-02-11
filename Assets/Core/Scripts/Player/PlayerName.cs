using TMPro;
using UnityEngine;

public class PlayerName : MonoBehaviour
{
    public Player player;
    public TextMeshPro text;

    private string lastPlayerName = "";

    void LateUpdate()
    {
        if (lastPlayerName != player.playerName)
        {
            lastPlayerName = player.playerName;
            text.text = player.playerName;
        }

        transform.rotation = Quaternion.LookRotation(-(GameManager.singleton.camera.transform.position - transform.position)); // why is it negative? i don't know.
    }

    private void OnValidate()
    {
        if (player == null)
            player = GetComponentInParent<Player>();
        if (text == null)
            text = GetComponent<TextMeshPro>();
    }
}
