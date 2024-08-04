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

    private ChatboxCommands commands;

    private void Start()
    {
        commands = GetComponent<ChatboxCommands>();
        input = GameManager.singleton.input;
        chatInput.gameObject.SetActive(false);

        chatInput.onSubmit.AddListener(OnChatBoxSubmitted);
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

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            chatInput.gameObject.SetActive(false);

        GameManager.singleton.SetInputBlockFlag(GameManager.InputBlockFlags.Chatbox, chatInput.gameObject.activeSelf);
    }

    public void OnChatBoxSubmitted(string text)
    {
        if (text != defaultText && !string.IsNullOrEmpty(text))
        {
            if (text.StartsWith("/"))
            {
                if (!commands.OnCommandSubmitted(text.Substring(1), out string error))
                    MessageFeed.PostLocal(error);
            }
            else
                Netplay.singleton.localClient?.CmdSendMessage(text);
        }

        chatInput.gameObject.SetActive(false);
    }
}
