using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClientPrefabSetup : MonoBehaviour
{
    public TMP_Text NicknameText;
    public TMP_Text IpText;

    public Button UpB;
    public Button DownB;
    public LobbyScript lobbyManager;
    public void Setup(String PlayerNickName, String PlayerIP)
    {
        NicknameText.text = PlayerNickName;
    }

    public void UpF()
    {
        
    }

    public void DownF()
    {
        
    }


}
