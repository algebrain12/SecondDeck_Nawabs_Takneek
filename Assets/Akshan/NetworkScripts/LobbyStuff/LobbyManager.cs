using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LobbyPost
{
    public string LobbyName;
    public NetworkClient HostDevice;
    public bool StatusChange;
}

[Serializable]
public class JoinRequest
{
    public NetworkClient ClientDevice;
    public string LobbyName;
}

[Serializable]
public class LeaveNotice
{
    public NetworkClient ClientDevice;
    public string LobbyName;
}

public class LobbyManager : MonoBehaviour
{
    public Dictionary<string, LobbyPost> LobbyList = new Dictionary<string, LobbyPost>();
    public Dictionary<string, NetworkClient> ConnectedClients = new Dictionary<string, NetworkClient>();

    [Header("UI Panels")]
    public GameObject MainLobbyPanel;   // Initial menu (Host / Client options)
    public GameObject HostPanel;        // Single panel for both Host Setup & Host Info
    public GameObject ClientLobbyPanel; // Client list view

    [Header("Host Controls inside HostPanel")]
    public GameObject SetupControlsGroup; // Container holding input fields & Create button (optional: hide after starting)
    public GameObject ClientListGroup;    // Container holding HostScroll & connected clients view

    [Header("Networking References")]
    public NetworkData SessionData;
    public UDPManager networkTypeManager;
    public UDPHandler uDPManager;

    [Header("Input Fields")]
    public TMP_InputField NickNameField;
    public TMP_InputField LobbyNameField;

    [Header("Scroll Area References (Canvas UI)")]
    public ScrollRect HostScroll;
    public ScrollRect ClientScroll;

    [Header("UI Prefabs & Displays")]
    public GameObject TextItemPrefab;
    public GameObject ButtonItemPrefab;
    public TMP_Text HostErrorText;

    [Header("Broadcast Settings")]
    [SerializeField] private float broadcastInterval = 1f;
    private float broadcastTimer = 0f;

    private bool isHosting = false;
    private string myLobbyName = null;
    private string joinedHostIP = null;
    private string joinedLobbyName = null;

    private bool needsClientScrollRefresh = false;
    private bool needsHostScrollRefresh = false;

    void Awake()
    {
        networkTypeManager.RegisterType<LobbyPost>("Lobby");
        networkTypeManager.Subscribe<LobbyPost>("Lobby", LobbyChange);

        networkTypeManager.RegisterType<JoinRequest>("Join");
        networkTypeManager.RegisterType<LeaveNotice>("Leave");
    }

    void Start()
    {
        ShowPanel(MainLobbyPanel);

        SessionData.ThisDevice = new NetworkClient
        {
            IPAddress = uDPManager.GetOwnIPAddress()
        };
    }

    void Update()
    {
        if (needsClientScrollRefresh)
        {
            needsClientScrollRefresh = false;
            RefreshClientScrollUI();
        }

        if (needsHostScrollRefresh)
        {
            needsHostScrollRefresh = false;
            RefreshHostScrollUI();
        }

        if (!isHosting) return;

        broadcastTimer += Time.deltaTime;
        if (broadcastTimer >= broadcastInterval)
        {
            broadcastTimer = 0f;
            LobbyPost post = new LobbyPost
            {
                LobbyName = myLobbyName,
                HostDevice = SessionData.ThisDevice,
                StatusChange = true
            };
            networkTypeManager.SendObject("Lobby", post);
        }
    }

    // ---------- UI Navigation ----------

    private void ShowPanel(GameObject targetPanel)
    {
        if (MainLobbyPanel != null) MainLobbyPanel.SetActive(targetPanel == MainLobbyPanel);
        if (HostPanel != null) HostPanel.SetActive(targetPanel == HostPanel);
        if (ClientLobbyPanel != null) ClientLobbyPanel.SetActive(targetPanel == ClientLobbyPanel);
    }

    // ---------- Host Flow ----------

    // 1. Hook up to the main menu "Host" button -> simply opens the HostPanel
    public void OnClickHost()
    {
        ShowPanel(HostPanel);

        // Reset host panel state when first entering
        if (SetupControlsGroup != null) SetupControlsGroup.SetActive(true);
        if (ClientListGroup != null) ClientListGroup.SetActive(true);
    }

    // 2. Hook up to the "Start Lobby / Broadcast" button inside the HostPanel
    public void StartBroadcasting()
    {
        string desiredName = LobbyNameField.text;

        if (string.IsNullOrWhiteSpace(desiredName))
        {
            ShowHostError("Lobby name can't be empty.");
            return;
        }

        if (LobbyList.TryGetValue(desiredName, out LobbyPost existing) && existing.StatusChange)
        {
            ShowHostError($"A lobby named \"{desiredName}\" already exists.");
            return;
        }

        SessionData.ThisDevice.Nickname = NickNameField.text;
        SessionData.ThisDevice.role = NetworkRole.Host;

        myLobbyName = desiredName;
        isHosting = true;

        networkTypeManager.Subscribe<JoinRequest>("Join", OnClientJoined);
        networkTypeManager.Subscribe<LeaveNotice>("Leave", OnClientLeft);

        ConnectedClients.Clear();
        RefreshHostScrollUI();

        // Optional: Hide the input fields/button once broadcasting starts so users can't edit mid-lobby
        if (SetupControlsGroup != null) SetupControlsGroup.SetActive(false);
    }

    private void OnClientJoined(JoinRequest req)
    {
        if (req.LobbyName != myLobbyName) return;

        lock (ConnectedClients)
        {
            ConnectedClients[req.ClientDevice.IPAddress] = req.ClientDevice;
        }
        needsHostScrollRefresh = true;
    }

    private void OnClientLeft(LeaveNotice notice)
    {
        if (notice.LobbyName != myLobbyName) return;

        lock (ConnectedClients)
        {
            ConnectedClients.Remove(notice.ClientDevice.IPAddress);
        }
        needsHostScrollRefresh = true;
    }

    private void RefreshHostScrollUI()
    {
        foreach (Transform child in HostScroll.content)
        {
            Destroy(child.gameObject);
        }

        lock (ConnectedClients)
        {
            foreach (NetworkClient client in ConnectedClients.Values)
            {
                GameObject item = Instantiate(TextItemPrefab, HostScroll.content);
                TMP_Text txt = item.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = client.Nickname;
            }
        }
    }

    private void ShowHostError(string message)
    {
        Debug.LogWarning($"[Lobby] {message}");
        if (HostErrorText != null) HostErrorText.text = message;
    }

    // ---------- Client Flow ----------

    public void OnClickClient()
    {
        SessionData.ThisDevice.Nickname = NickNameField.text;
        SessionData.ThisDevice.role = NetworkRole.MobileClient;

        ShowPanel(ClientLobbyPanel);
        RefreshClientScrollUI();
    }

    private void RefreshClientScrollUI()
    {
        foreach (Transform child in ClientScroll.content)
        {
            Destroy(child.gameObject);
        }

        lock (LobbyList)
        {
            foreach (LobbyPost post in LobbyList.Values)
            {
                if (!post.StatusChange) continue;

                GameObject item = Instantiate(ButtonItemPrefab, ClientScroll.content);

                TMP_Text txt = item.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = post.LobbyName;

                Button btn = item.GetComponent<Button>();
                LobbyPost currentPost = post;
                if (btn != null) btn.onClick.AddListener(() => JoinLobby(currentPost));
                else Debug.Log("fun??");
            }
        }
    }

    private void JoinLobby(LobbyPost post)
    {
        joinedHostIP = post.HostDevice.IPAddress;
        joinedLobbyName = post.LobbyName;

        JoinRequest req = new JoinRequest
        {
            ClientDevice = SessionData.ThisDevice,
            LobbyName = post.LobbyName
        };

        networkTypeManager.SendObject("Join", req, joinedHostIP);
        ShowPanel(null);
    }

    // ---------- Network Callbacks ----------

    public void LobbyChange(LobbyPost post)
    {
        lock (LobbyList)
        {
            if (post.StatusChange)
            {
                LobbyList[post.LobbyName] = post;
            }
            else
            {
                LobbyList.Remove(post.LobbyName);
            }
        }

        needsClientScrollRefresh = true;
    }

    // ---------- Cleanup ----------

    void OnApplicationQuit()
    {
        if (isHosting && myLobbyName != null)
        {
            LobbyPost closePost = new LobbyPost
            {
                LobbyName = myLobbyName,
                HostDevice = SessionData.ThisDevice,
                StatusChange = false
            };
            networkTypeManager.SendObject("Lobby", closePost);
        }
        else if (joinedHostIP != null && joinedLobbyName != null)
        {
            LeaveNotice notice = new LeaveNotice
            {
                ClientDevice = SessionData.ThisDevice,
                LobbyName = joinedLobbyName
            };
            networkTypeManager.SendObject("Leave", notice, joinedHostIP);
        }
    }
}