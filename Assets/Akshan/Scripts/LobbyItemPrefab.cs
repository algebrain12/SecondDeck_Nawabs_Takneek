using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyItemPrefab : MonoBehaviour
{
    public TMP_Text LobbyNameText;
    public TMP_Text LobbyIpText;
    public Button LobbyJoinButton;

    public TMP_InputField nickname;
    public String LobbyIp;

    public LobbyScript lobbyManager;
    public void Setup(String LobbyName, String LobbyIP)
    {
        LobbyNameText.text = LobbyName;
        LobbyJoinButton.onClick.RemoveAllListeners();
        LobbyJoinButton.onClick.AddListener(OnClickTheButton);
        LobbyIp = LobbyIP;
    }

    private void OnClickTheButton()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("[LobbyItemPrefab] lobbyManager reference is null — was it assigned after Instantiate?");
            return;
        }
        lobbyManager.OnHostClick(LobbyIp, nickname.text);
    }
}
