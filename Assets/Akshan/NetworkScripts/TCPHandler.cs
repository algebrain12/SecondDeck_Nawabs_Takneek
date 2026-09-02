using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class TCPHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TCPManager typeManager;

    [Header("Network Settings")]
    [SerializeField] private int listenPort = 9100;

    public enum Mode { Server, Client, None }
    [Header("Role")]
    [SerializeField] private Mode mode = Mode.None;

    // ---------- Server-side state ----------
    private TcpListener tcpListener;
    private Thread acceptThread;
    private volatile bool isServerRunning = false;

    // Connected clients, keyed by remote IP. ConcurrentDictionary because
    // Accept/Read threads and the main thread all touch this.
    private readonly ConcurrentDictionary<string, ClientConnection> connectedClients
        = new ConcurrentDictionary<string, ClientConnection>();

    // ---------- Client-side state ----------
    public ClientConnection serverConnection; // this device's connection to the host, if in Client mode

    // Background thread(s) -> main thread handoff, same pattern as UDPManager.
    private readonly ConcurrentQueue<string> receiveQueue = new ConcurrentQueue<string>();

    // Lets background threads (e.g. the connect thread) queue work to run
    // on the main thread, since Unity APIs / most of our own code isn't thread-safe.
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    // Fired (on the main thread) once ConnectToServer() actually establishes
    // a connection. Subscribe to this before calling ConnectToServer() if you
    // need to send something that depends on the connection being live —
    // sending immediately after calling ConnectToServer() races its background
    // connect thread and will often lose, since Connect() is real network I/O
    // that doesn't complete instantly.
    public event Action OnConnectedToServer;

    // Fired (on the main thread) if ConnectToServer() exhausts all of its
    // retry attempts without succeeding, so callers can show an error/timeout
    // instead of waiting forever on a connection that will never come.
    public event Action<string> OnConnectionFailed;

    // Lets callers check the current role before calling StartAsServer()/
    // ConnectToServer() again, instead of relying on the "Already started" warning.
    public bool IsServer => mode == Mode.Server;
    public bool IsClient => mode == Mode.Client;

    // Wraps a single TCP connection: the socket, its stream, a write lock
    // (so concurrent sends don't interleave bytes on the wire), and its
    // receive thread.
    public class ClientConnection
    {
        public TcpClient client;
        public NetworkStream stream;
        public readonly object writeLock = new object();
        public Thread receiveThread;
        public string ip;
    }

    // ---------- Lifecycle ----------

    private void Update()
    {
        while (receiveQueue.TryDequeue(out string message))
        {
            typeManager.HandleRaw(message);
        }

        // Drain any callbacks queued from background threads.
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    private void OnDestroy() => CloseAll();
    private void OnApplicationQuit() => CloseAll();

    // ---------- Public API: role setup ----------

    // Call this on the device acting as host (e.g. the PC).
    public void StartAsServer()
    {
        if (mode != Mode.None)
        {
            Debug.LogWarning("[TCP] Already started; call CloseAll() before switching roles.");
            return;
        }

        mode = Mode.Server;

        try
        {
            tcpListener = new TcpListener(IPAddress.Any, listenPort);
            tcpListener.Start();
            isServerRunning = true;

            acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            acceptThread.Start();

            Debug.Log($"[TCP] Server listening on port {listenPort}...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCP Server Start Error] {ex.Message}");
            mode = Mode.None;
        }
    }

    // Call this on the device acting as a client (e.g. the phone), once you
    // know the server's IP (e.g. from your existing UDP handshake/discovery).
    public void ConnectToServer(string serverIP, int maxAttempts = 4, int delayMs = 500)
    {
        if (mode != Mode.None)
        {
            Debug.LogWarning("[TCP] Already started; call CloseAll() before switching roles.");
            return;
        }

        mode = Mode.Client;

        Thread connectThread = new Thread(() =>
        {
            Exception lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    TcpClient client = new TcpClient();
                    client.Connect(serverIP, listenPort);

                    serverConnection = new ClientConnection
                    {
                        client = client,
                        stream = client.GetStream(),
                        ip = serverIP
                    };

                    serverConnection.receiveThread = new Thread(() => ReceiveLoop(serverConnection))
                    {
                        IsBackground = true
                    };
                    serverConnection.receiveThread.Start();

                    Debug.Log($"[TCP] Connected to server {serverIP}:{listenPort} on attempt {attempt}.");

                    // Notify subscribers that the connection is actually
                    // live, marshalled onto the main thread.
                    mainThreadActions.Enqueue(() => OnConnectedToServer?.Invoke());
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.LogWarning($"[TCP Connect Attempt {attempt}/{maxAttempts} failed] {ex.Message}");
                    Thread.Sleep(delayMs);
                }
            }

            Debug.LogError($"[TCP Connect Error] Giving up after {maxAttempts} attempts: {lastError?.Message}");
            mode = Mode.None;

            // Notify subscribers that the connection permanently failed.
            string errorMessage = lastError?.Message ?? "Unknown error";
            mainThreadActions.Enqueue(() => OnConnectionFailed?.Invoke(errorMessage));
        })
        { IsBackground = true };

        connectThread.Start();
    }

    // ---------- Public API (used by NetworkTypeManagerTCP) ----------

    // Sends to a specific IP. Server mode: looks up that client's connection.
    // Client mode: the ip argument is ignored (a client only has one peer:
    // the server), but kept for signature symmetry with UDPManager.SendUDP2.
    public void SendTCP2(string message, string ip)
    {
        if (mode == Mode.Server)
        {
            if (connectedClients.TryGetValue(ip, out ClientConnection conn))
            {
                WriteFramed(conn, message);
            }
            else
            {
                Debug.LogWarning($"[TCP] No connected client with IP {ip}.");
            }
        }
        else if (mode == Mode.Client)
        {
            WriteFramed(serverConnection, message);
        }
    }

    // Sends to "the default destination": server mode broadcasts to every
    // connected client (TCP has no native broadcast, so this loops over
    // all connections); client mode sends to the server.
    public void SendTCP(string message)
    {
        if (mode == Mode.Server)
        {
            foreach (ClientConnection conn in connectedClients.Values)
            {
                WriteFramed(conn, message);
            }
        }
        else if (mode == Mode.Client)
        {
            if (serverConnection == null)
            {
                Debug.LogWarning("[TCP] Not connected to a server yet.");
                return;
            }
            WriteFramed(serverConnection, message);
        }
        else
        {
            Debug.LogWarning("[TCP] SendTCP called before StartAsServer()/ConnectToServer().");
        }
    }

    public bool IsConnected => mode == Mode.Server
        ? connectedClients.Count > 0
        : serverConnection != null && serverConnection.client.Connected;

    public IEnumerable<string> ConnectedClientIPs => connectedClients.Keys;

    // ---------- Server: accept loop ----------

    private void AcceptLoop()
    {
        while (isServerRunning)
        {
            try
            {
                TcpClient incoming = tcpListener.AcceptTcpClient(); // blocks until a client connects
                string ip = ((IPEndPoint)incoming.Client.RemoteEndPoint).Address.ToString();

                ClientConnection conn = new ClientConnection
                {
                    client = incoming,
                    stream = incoming.GetStream(),
                    ip = ip
                };

                connectedClients[ip] = conn;

                conn.receiveThread = new Thread(() => ReceiveLoop(conn)) { IsBackground = true };
                conn.receiveThread.Start();

                Debug.Log($"[TCP] Client connected: {ip}");
            }
            catch (SocketException)
            {
                // Expected when tcpListener.Stop() is called during shutdown.
                if (isServerRunning)
                    Debug.LogWarning("[TCP] Accept loop socket exception during active run.");
            }
            catch (Exception ex)
            {
                if (isServerRunning)
                    Debug.LogError($"[TCP Accept Error] {ex.Message}");
            }
        }
    }

    // ---------- Framed send/receive ----------

    // Writes a 4-byte length prefix followed by the UTF8 payload. The lock
    // ensures the prefix and payload for one message are never interleaved
    // with another thread's send on the same connection.
    private void WriteFramed(ClientConnection conn, string message)
    {
        if (conn == null || conn.stream == null) return;

        try
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

            lock (conn.writeLock)
            {
                conn.stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                conn.stream.Write(payload, 0, payload.Length);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TCP Send Error to {conn.ip}] {ex.Message}");
            RemoveConnection(conn);
        }
    }

    // Reads exactly 'count' bytes into buffer, looping because a single
    // stream.Read() call is not guaranteed to return all requested bytes
    // at once. Returns false if the connection closed mid-read.
    private bool ReadExact(NetworkStream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read == 0) return false; // remote closed the connection
            offset += read;
        }
        return true;
    }

    private void ReceiveLoop(ClientConnection conn)
    {
        byte[] lengthBuffer = new byte[4];

        while (true)
        {
            try
            {
                if (!ReadExact(conn.stream, lengthBuffer, 4)) break;

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                if (messageLength < 0 || messageLength > 10_000_000)
                {
                    Debug.LogWarning($"[TCP] Implausible frame length {messageLength} from {conn.ip}, dropping connection.");
                    break;
                }

                byte[] payloadBuffer = new byte[messageLength];
                if (!ReadExact(conn.stream, payloadBuffer, messageLength)) break;

                string text = Encoding.UTF8.GetString(payloadBuffer);
                receiveQueue.Enqueue(text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TCP Receive Error from {conn.ip}] {ex.Message}");
                break;
            }
        }

        Debug.Log($"[TCP] Connection closed: {conn.ip}");
        RemoveConnection(conn);
    }

    private void RemoveConnection(ClientConnection conn)
    {
        if (conn == null) return;

        try { conn.stream?.Close(); } catch { }
        try { conn.client?.Close(); } catch { }

        if (mode == Mode.Server)
        {
            connectedClients.TryRemove(conn.ip, out _);
        }
        else if (mode == Mode.Client && serverConnection == conn)
        {
            serverConnection = null;
        }
    }

    // ---------- Shutdown ----------

    private void CloseAll()
    {
        isServerRunning = false;

        try { tcpListener?.Stop(); } catch { }
        tcpListener = null;

        if (acceptThread != null && acceptThread.IsAlive)
            acceptThread.Join(500);
        acceptThread = null;

        foreach (ClientConnection conn in connectedClients.Values)
            RemoveConnection(conn);
        connectedClients.Clear();

        if (serverConnection != null)
            RemoveConnection(serverConnection);

        mode = Mode.None;
    }
}