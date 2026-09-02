using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum LobbyType
{
    OnePlayer,
    TwoPlayer
}

public class LobbyPostRequest
{
    public String DeviceIpAdd;
    public String LobbyName;

    public bool Equals(LobbyPostRequest other) =>
        other != null && DeviceIpAdd == other.DeviceIpAdd;

    public override bool Equals(object obj) => Equals(obj as LobbyPostRequest);
    public override int GetHashCode() => DeviceIpAdd?.GetHashCode() ?? 0;

}

public class ClientJoinRequest
{
    public String DeviceIPAdd;
    public String Nickname;
}

public class GameStartRequest
{
    public PlayerRole role;
    public String SceneRequest;
    public Ability ability;
}

public class LobbyScript : MonoBehaviour
{

    public GameObject HostPanel1;
    public GameObject HostPanel2;
    public GameObject ClientPanel1;
    public GameObject ClientPanel2;
    public Transform HostContent;
    public Transform ClientContent;

    public GameObject LobbyItemPrefab;
    public GameObject ClientItemPrefab;

    public TCPManager tcpManager;
    public TCPHandler tcpHandler;
    public GameObject AndroidBackgroundPanel;

    public LobbyType lobbyType = LobbyType.OnePlayer;
    public UDPManager udpManager;
    public UDPHandler udpHandler;

    public TMP_InputField LobbyNameInput;
    public TMP_Text LobbyNameText;

    public TMP_InputField nicknameField;

    public AllPlayerData playerData;
    public ServerInfo serverInfo;

    public SelfData selfData;

    [Header("Connection Status (optional)")]
    [Tooltip("Optional UI text shown while attempting to connect / on failure. Safe to leave unassigned.")]
    public TMP_Text connectionStatusText;

    private String LobbyName = "example";
    private String GameSceneName = "GameScene";
    private bool IsBroadCastingLobby = false;
    [SerializeField]public List<Button> buttons;

    public GameObject t1;
    public GameObject t2;
    public Button b1;
    public Button b2;

    private Dictionary<string, GameObject> LobbyItems = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> ClientItems = new Dictionary<string, GameObject>();


    
    void Start()
    {
        udpManager.RegisterType<LobbyPostRequest>("LobbyPostRequest");

        // NEW: benchmark echo endpoint, alive as soon as the Lobby scene's
        // network managers exist - covers both the host panel and client
        // panel, since both share this same script/these same manager refs.
        BenchmarkEndpoint.RegisterOn(udpManager, tcpManager);

        HostPanel2.SetActive(false);
        ClientPanel2.SetActive(false);
        #if UNITY_ANDROID && !UNITY_EDITOR
            HostPanel1.SetActive(false);
            AndroidBackgroundPanel.SetActive(true);
            ClientPanel1.SetActive(true);
            udpManager.Subscribe<LobbyPostRequest>("LobbyPostRequest", GetLobbyMessage);
            return;
        #endif
        ClientPanel1.SetActive(false);
        HostPanel1.SetActive(true);
    }

    public void ChangeGameSceneTo1()
    {
        GameSceneName = "GameScene";
    }

    public void ChangeGameSceneTo2()
    {
        GameSceneName = "GameScene2";
    }

    public void ChangeScene(GameObject gameObject)
    {
        GameSceneName = gameObject.name;
        foreach(Button b in buttons)
        {
            b.interactable = true;
        }
        gameObject.GetComponent<Button>().interactable = false;

    }
    

    private void GetLobbyMessage(LobbyPostRequest postRequest)
    {
        if (LobbyItems.ContainsKey(postRequest.DeviceIpAdd))
        {
            LobbyItems[postRequest.DeviceIpAdd]
                .GetComponent<LobbyItemPrefab>()
                .Setup(postRequest.LobbyName, postRequest.DeviceIpAdd);
            return;
        }

        GameObject newItem = Instantiate(LobbyItemPrefab, ClientContent);
        LobbyItemPrefab itemScript = newItem.GetComponent<LobbyItemPrefab>();
        itemScript.lobbyManager = this;
        itemScript.nickname = nicknameField;
        itemScript.Setup(postRequest.LobbyName, postRequest.DeviceIpAdd);
        LobbyItems[postRequest.DeviceIpAdd] = newItem;
    }

    public void BackToLobby()
    {
        SceneManager.LoadScene("Final_Lobby");
    }

    public void OnHostClick(String LobbyIP, String NickName)
    {
        serverInfo.ServerIP = LobbyIP;
        selfData.IPAddress = udpHandler.GetOwnIPAddress();
        selfData.NickName = NickName;

        tcpManager.RegisterType<ClientJoinRequest>("JoinRequest");
        tcpManager.RegisterType<GameStartRequest>("startReq");
        tcpManager.Subscribe<GameStartRequest>("startReq", ChangeScene);

        ClientPanel1.SetActive(false);
        ClientPanel2.SetActive(true);

        if (connectionStatusText != null)
            connectionStatusText.text = $"Connecting to {LobbyIP}...";

        // Wait for the real TCP connection before sending JoinRequest.
        // Sending immediately after ConnectToServer() races its background
        // connect thread and usually loses (Connect() is real network I/O,
        // not instant) — the join request was being silently dropped even
        // though the socket went on to connect a moment later.
        tcpHandler.OnConnectedToServer += HandleConnectedToServer;
        tcpHandler.OnConnectionFailed += HandleConnectionFailed;

        tcpHandler.ConnectToServer(LobbyIP);
    }

    private void HandleConnectedToServer()
    {
        tcpHandler.OnConnectedToServer -= HandleConnectedToServer;
        tcpHandler.OnConnectionFailed -= HandleConnectionFailed;

        if (connectionStatusText != null)
            connectionStatusText.text = "Connected. Joining lobby...";

        ClientJoinRequest joinRequest = new ClientJoinRequest()
        {
            DeviceIPAdd = selfData.IPAddress,
            Nickname = selfData.NickName
        };
        tcpManager.SendObject<ClientJoinRequest>("JoinRequest", joinRequest);
    }

    private void HandleConnectionFailed(string errorMessage)
    {
        tcpHandler.OnConnectedToServer -= HandleConnectedToServer;
        tcpHandler.OnConnectionFailed -= HandleConnectionFailed;

        Debug.LogError($"[Lobby] Failed to connect: {errorMessage}");

        if (connectionStatusText != null)
            connectionStatusText.text = "Failed to connect. Please try again.";

        // Let the player retry instead of being stuck on the "connecting" panel.
        ClientPanel2.SetActive(false);
        ClientPanel1.SetActive(true);
    }

    private void ChangeScene(GameStartRequest startRequest)
    {
        serverInfo.LobbyName = LobbyName;
        selfData.ability = startRequest.ability;
        SceneManager.LoadScene(startRequest.SceneRequest);
    }

    public void OnClickStart()
    {
        tcpManager.RegisterType<GameStartRequest>("startReq");
        playerData.playerList = new List<PlayerData>();
        playerData.lobbyType = lobbyType;
        int shipindex = 0;

        if(lobbyType == LobbyType.TwoPlayer)
        {
            int kk = 0;
            foreach(var (ip, obj) in ClientItems)
            {
                PlayerData pData = new PlayerData();
                pData.IPAddress = ip;
                pData.NickName = obj.GetComponent<ClientPrefabSetup>().NicknameText.text;
                pData.ShipIndex = shipindex;
                if(kk%2 == 1)pData.playerRole = PlayerRole.P2;
                else pData.playerRole = PlayerRole.P1;

                // FIX: pData.ability must be assigned BEFORE it's read into
                // startReq.ability below. Previously startReq.ability copied
                // pData.ability's default (unset) value first, and the random
                // roll happened a line too late — so every P2 client was sent
                // the wrong (always-default) ability instead of the rolled one.
                if(kk%2 == 1){
                    pData.ability = AbilityUtils.GetRandom();
                }

                playerData.playerList.Add(pData);
                GameStartRequest startReq = new GameStartRequest();
                startReq.role = pData.playerRole;
                startReq.ability = pData.ability;
                if(kk%2 == 1)startReq.SceneRequest = "Phone2Scene";
                else startReq.SceneRequest = "Phone1Scene";
                tcpManager.SendObject<GameStartRequest>("startReq", startReq, ip);
                if(kk%2 == 1)shipindex++;
                kk++;
            }
            SceneManager.LoadScene(GameSceneName);
            return;
        }

        foreach(var (ip, obj) in ClientItems)
        {
            PlayerData pData = new PlayerData();
            pData.IPAddress = ip;
            pData.NickName = obj.GetComponent<ClientPrefabSetup>().NicknameText.text;
            pData.ShipIndex = shipindex;
            pData.playerRole = PlayerRole.SoleController;
            pData.ability = AbilityUtils.GetRandom();
            playerData.playerList.Add(pData);
            GameStartRequest startReq = new GameStartRequest();
            startReq.role = pData.playerRole;
            startReq.SceneRequest = "CombinedScene";
            startReq.ability = pData.ability;
            tcpManager.SendObject<GameStartRequest>("startReq", startReq, ip);
            shipindex++;
        }
        SceneManager.LoadScene(GameSceneName);
    }

    public void ChangetoOne()
    {
        lobbyType = LobbyType.OnePlayer;
        t1.SetActive(true);
        t2.SetActive(false);
        b1.interactable = false;b2.interactable = true;
    }
    public void ChangetoTwo()
    {
        lobbyType = LobbyType.TwoPlayer;
        t1.SetActive(false);
        t2.SetActive(true);
        b2.interactable = false;b1.interactable = true;
    }

    public void OnClickHostButton()
    {
        tcpHandler.StartAsServer();
        LobbyName = LobbyNameInput.text;
        HostPanel1.SetActive(false);
        HostPanel2.SetActive(true);
        LobbyNameText.text = LobbyName;
        IsBroadCastingLobby = true;
        tcpManager.RegisterType<ClientJoinRequest>("JoinRequest");
        tcpManager.Subscribe<ClientJoinRequest>("JoinRequest", ClientRequestIntercept);
    }

    private void ClientRequestIntercept(ClientJoinRequest joinRequest)
    {
        if (ClientItems.ContainsKey(joinRequest.DeviceIPAdd))
        {
            ClientItems[joinRequest.DeviceIPAdd]
                .GetComponent<ClientPrefabSetup>()
                .Setup(joinRequest.Nickname, joinRequest.DeviceIPAdd);
            return;
        }

        GameObject newItem = Instantiate(ClientItemPrefab, HostContent);
        newItem.GetComponent<ClientPrefabSetup>().lobbyManager = this;
        newItem.GetComponent<ClientPrefabSetup>().Setup(joinRequest.Nickname, joinRequest.DeviceIPAdd);
        ClientItems[joinRequest.DeviceIPAdd] = newItem;
    }

    void Update()
    {
        if (IsBroadCastingLobby)
        {
            LobbyPostRequest lobbyPostRequest = new LobbyPostRequest
            {
                DeviceIpAdd = udpHandler.GetOwnIPAddress(),
                LobbyName = LobbyName
            };
            udpManager.SendObject<LobbyPostRequest>("LobbyPostRequest", lobbyPostRequest);
        }
    }
}