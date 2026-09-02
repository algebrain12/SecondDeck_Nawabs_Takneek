using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UDPManager typeManager;

    [Header("Network Settings")]
    [SerializeField] private int listenPort = 9000;
    [SerializeField] private int remotePort = 9000;

    // Default destination used when SendUDP() is called without an explicit IP.
    // Starts as the subnet broadcast address so initial discovery/handshake works.
    private string defaultRemoteIP;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning = false;

    // Background thread -> main thread handoff
    private readonly ConcurrentQueue<string> receiveQueue = new ConcurrentQueue<string>();

    // ---------- Lifecycle ----------
    public string broadcastAddress = "None";

    private void Start()
    {
        // Initialize client first
        udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        // Resolve broadcast IP
        broadcastAddress = GetSubnetBroadcastAddress();
        
        Debug.Log($"[UDP] Initialized. Broadcast Target: {broadcastAddress}");
        defaultRemoteIP = GetSubnetBroadcastAddress();
        StartListener();
    }

    private void Update()
    {
        while (receiveQueue.TryDequeue(out string message))
        {
            typeManager.HandleRaw(message);
        }
    }

    private void OnDestroy() => CloseUDP();
    private void OnApplicationQuit() => CloseUDP();

    // ---------- Public API (used by NetworkTypeManager) ----------

    // Sends a raw wire string to a specific IP.
    public void SendUDP2(string message, string ip)
    {
        SendRaw(message, ip);
        Debug.Log(ip);
    }

    // Sends a raw wire string to the default destination (subnet broadcast, unless changed).
    public void SendUDP(string message)
    {
        SendRaw(message, defaultRemoteIP);
        Debug.Log(defaultRemoteIP);
    }

    // Call this once a handshake succeeds, so future undirected sends go straight to the peer
    // instead of broadcasting.
    public void SetDefaultRemoteIP(string ip)
    {
        defaultRemoteIP = ip;
    }

    // ---------- Send ----------

    private void SendRaw(string message, string ip)
    {
        if (udpClient == null || string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("[UDP] Cannot send: client not ready or IP is empty.");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udpClient.Send(data, data.Length, ip, remotePort);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP Send Error] {ex.Message}");
        }
    }

    // ---------- Receive (background thread) ----------

    private void StartListener()
    {
        try
        {
            udpClient = new UdpClient(listenPort) { EnableBroadcast = true };
            isRunning = true;

            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();

            Debug.Log($"[UDP] Listening on port {listenPort}...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UDP Binding Error] {ex.Message}");
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string text = Encoding.UTF8.GetString(data);
                receiveQueue.Enqueue(text);
            }
            catch (SocketException ex)
            {
                if (isRunning)
                    Debug.LogWarning($"[UDP Socket Exception] {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UDP Receive Error] {ex.Message}");
            }
        }
    }

    private void CloseUDP()
    {
        isRunning = false;

        udpClient?.Close();
        udpClient = null;

        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(500);

        receiveThread = null;
    }

    // ---------- Subnet broadcast helper ----------

    public string GetSubnetBroadcastAddress()
    {
    #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject connectivityManager = activity.Call<AndroidJavaObject>("getSystemService", "connectivity"))
            {
                // Get all networks currently available to the OS
                AndroidJavaObject[] networks = connectivityManager.Call<AndroidJavaObject[]>("getAllNetworks");

                foreach (AndroidJavaObject network in networks)
                {
                    AndroidJavaObject capabilities = connectivityManager.Call<AndroidJavaObject>("getNetworkCapabilities", network);
                    if (capabilities == null) continue;

                    // TRANSPORT_WIFI = 1 (android.net.NetworkCapabilities.TRANSPORT_WIFI)
                    bool isWifi = capabilities.Call<bool>("hasTransport", 1);
                    if (!isWifi) continue;

                    AndroidJavaObject linkProperties = connectivityManager.Call<AndroidJavaObject>("getLinkProperties", network);
                    if (linkProperties == null) continue;

                    AndroidJavaObject[] linkAddresses = linkProperties.Call<AndroidJavaObject[]>("getLinkAddresses");

                    foreach (AndroidJavaObject linkAddress in linkAddresses)
                    {
                        AndroidJavaObject address = linkAddress.Call<AndroidJavaObject>("getAddress");
                        string ipString = address.Call<string>("getHostAddress");

                        if (!IPAddress.TryParse(ipString, out IPAddress ip)) continue;
                        if (ip.AddressFamily != AddressFamily.InterNetwork) continue; // skip IPv6

                        int prefixLength = linkAddress.Call<int>("getPrefixLength");

                        byte[] ipBytes = ip.GetAddressBytes();
                        byte[] maskBytes = PrefixLengthToMask(prefixLength);
                        byte[] broadcastBytes = new byte[4];

                        for (int i = 0; i < 4; i++)
                            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

                        string result = new IPAddress(broadcastBytes).ToString();
                        Debug.Log($"[UDP] Android Wi-Fi broadcast resolved via LinkProperties: {result} (IP {ipString}/{prefixLength})");
                        return result;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UDP] Android LinkProperties broadcast lookup failed: {ex.Message}");
        }

        return "10.203.162.255";
    #endif

        // Cross-platform: find the IP the OS would actually use for outbound traffic.
        IPAddress localIP = GetActiveLocalIPAddress();
        if (localIP == null)
        {
            Debug.LogWarning("[UDP] Could not determine active local IP.");
            return "255.255.255.255";
        }

        // Match that IP to its NetworkInterface to get the correct subnet mask.
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;

            IPInterfaceProperties props = ni.GetIPProperties();
            foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
            {
                if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (!ip.Address.Equals(localIP)) continue;
                if (ip.IPv4Mask == null) continue;

                byte[] ipBytes = ip.Address.GetAddressBytes();
                byte[] maskBytes = ip.IPv4Mask.GetAddressBytes();
                byte[] broadcastBytes = new byte[4];

                for (int i = 0; i < 4; i++)
                    broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);

                string result = new IPAddress(broadcastBytes).ToString();
                Debug.Log($"[UDP] Broadcast resolved via active route: {result} (local IP {localIP})");
                return result;
            }
        }

        // Fallback: assume /24 if we found the IP but couldn't match a mask.
        byte[] fallbackBytes = localIP.GetAddressBytes();
        fallbackBytes[3] = 255;
        string fallback = new IPAddress(fallbackBytes).ToString();
        Debug.LogWarning($"[UDP] No matching NetworkInterface mask found, assuming /24: {fallback}");
        return "10.203.162.255";
    }

    // Uses a UDP "connect" (no data sent) to ask the OS which local interface/IP
    // it would route through to reach an external address. This reliably skips
    // virtual adapters (VPN, Hyper-V, VMware, Docker, etc.) that a raw
    // NetworkInterface enumeration can't distinguish from the real one.
    private IPAddress GetActiveLocalIPAddress()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                // 8.8.8.8 is just used for route resolution; nothing is actually sent.
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UDP] Route resolution failed: {ex.Message}");
            return null;
        }
    }

    private byte[] PrefixLengthToMask(int prefixLength)
    {
        uint mask = prefixLength == 0 ? 0 : 0xFFFFFFFF << (32 - prefixLength);
        return new byte[]
        {
            (byte)((mask >> 24) & 0xFF),
            (byte)((mask >> 16) & 0xFF),
            (byte)((mask >> 8) & 0xFF),
            (byte)(mask & 0xFF)
        };
    }

    public string GetOwnIPAddress()
    {
    #if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject connectivityManager = activity.Call<AndroidJavaObject>("getSystemService", "connectivity"))
            {
                AndroidJavaObject[] networks = connectivityManager.Call<AndroidJavaObject[]>("getAllNetworks");

                foreach (AndroidJavaObject network in networks)
                {
                    AndroidJavaObject capabilities = connectivityManager.Call<AndroidJavaObject>("getNetworkCapabilities", network);
                    if (capabilities == null) continue;

                    // TRANSPORT_WIFI = 1. Deliberately excludes TRANSPORT_VPN (4) and
                    // TRANSPORT_CELLULAR (0), so this naturally skips VPN tunnels
                    // and mobile data — Android's equivalent of "virtual adapters."
                    bool isWifi = capabilities.Call<bool>("hasTransport", 1);
                    if (!isWifi) continue;

                    AndroidJavaObject linkProperties = connectivityManager.Call<AndroidJavaObject>("getLinkProperties", network);
                    if (linkProperties == null) continue;

                    AndroidJavaObject[] linkAddresses = linkProperties.Call<AndroidJavaObject[]>("getLinkAddresses");

                    foreach (AndroidJavaObject linkAddress in linkAddresses)
                    {
                        AndroidJavaObject address = linkAddress.Call<AndroidJavaObject>("getAddress");
                        string ipString = address.Call<string>("getHostAddress");

                        if (!IPAddress.TryParse(ipString, out IPAddress ip)) continue;
                        if (ip.AddressFamily != AddressFamily.InterNetwork) continue;

                        Debug.Log($"[UDP] Android own IP resolved via LinkProperties (Wi-Fi only): {ipString}");
                        return ipString;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UDP] Android LinkProperties own-IP lookup failed: {ex.Message}");
        }
    #endif

        string physicalIP = GetPhysicalAdapterIP();
        if (physicalIP != null)
        {
            Debug.Log($"[UDP] Own IP resolved via physical adapter scan: {physicalIP}");
            return physicalIP;
        }

        // Last resort: whatever the OS routing table says, even if it's virtual.
        IPAddress routedIP = GetActiveLocalIPAddress();
        if (routedIP != null)
        {
            Debug.LogWarning($"[UDP] No physical adapter matched; falling back to routed IP (may be virtual): {routedIP}");
            return routedIP.ToString();
        }

        Debug.LogWarning("[UDP] Could not determine own local IP.");
        return null;
    }

    // Enumerates real network interfaces only, explicitly rejecting known
    // virtual/tunnel/VPN adapter signatures. Prefers interfaces with an active
    // gateway (a strong signal of "this is the real uplink"), and prefers
    // Wireless80211/Ethernet types over anything else.
    private string GetPhysicalAdapterIP()
    {
        // Substrings commonly found in virtual adapter Name/Description on Windows/Mac/Linux.
        string[] virtualSignatures =
        {
            "virtual", "vethernet", "vmware", "virtualbox", "hyper-v", "hyperv",
            "docker", "vpn", "tap", "tun", "loopback", "pseudo", "wsl",
            "bluetooth", "npcap", "teredo", "isatap"
        };

        NetworkInterface bestCandidate = null;
        int bestScore = -1;

        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            string nameCheck = (ni.Name + " " + ni.Description).ToLowerInvariant();
            bool looksVirtual = false;
            foreach (string sig in virtualSignatures)
            {
                if (nameCheck.Contains(sig))
                {
                    looksVirtual = true;
                    break;
                }
            }
            if (looksVirtual) continue;

            IPInterfaceProperties props = ni.GetIPProperties();
            bool hasIPv4 = false;
            foreach (UnicastIPAddressInformation ip in props.UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    hasIPv4 = true;
                    break;
                }
            }
            if (!hasIPv4) continue;

            // Score candidates: real Wi-Fi/Ethernet with a gateway wins.
            int score = 0;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 3;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 2;
            if (props.GatewayAddresses.Count > 0) score += 5;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = ni;
            }
        }

        if (bestCandidate == null) return null;

        foreach (UnicastIPAddressInformation ip in bestCandidate.GetIPProperties().UnicastAddresses)
        {
            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                return ip.Address.ToString();
        }

        return null;
    }
}