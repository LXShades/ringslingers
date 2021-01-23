using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class Chatbox : MonoBehaviour
{
    [FormerlySerializedAs("input")]
    public InputField chatInput;

    public string defaultText = "<type here>";

    private PlayerControls input;

    private void Start()
    {
        input = GameManager.singleton.input;
        chatInput.gameObject.SetActive(false);

        chatInput.onEndEdit.AddListener(OnChatBoxSubmitted);
    }

    private void Update()
    {
        if (!chatInput.gameObject.activeSelf && input.Gameplay.Talk.triggered)
        {
            chatInput.gameObject.SetActive(true);
            chatInput.text = "<type here>";
            chatInput.ActivateInputField();
            chatInput.Select();
        }

        if (chatInput.gameObject.activeInHierarchy)
            GameManager.singleton.SuppressGameplayInputs();
    }

    public void OnChatBoxSubmitted(string text)
    {
        if (Keyboard.current.enterKey.wasPressedThisFrame && text != defaultText && !string.IsNullOrEmpty(text))
        {
            Netplay.singleton.localClient.CmdSendMessage(text);
        }

        chatInput.gameObject.SetActive(false);
    }
}
