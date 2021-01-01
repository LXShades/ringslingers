﻿using UnityEngine;
using UnityEngine.UI;

public class Chatbox : MonoBehaviour
{
    public InputField input;

    private void Start()
    {
        input.gameObject.SetActive(false);

        input.onEndEdit.AddListener(OnChatBoxSubmitted);
    }

    private void Update()
    {
        if (!input.gameObject.activeSelf && Input.GetButtonDown("Talk"))
        {
            input.gameObject.SetActive(true);
            input.text = "<type here>";
            input.ActivateInputField();
            input.Select();
        }
    }

    public void OnChatBoxSubmitted(string text)
    {
        if (Input.GetKey(KeyCode.Return))
            Log.Write(input.text);

        input.gameObject.SetActive(false);
    }
}
