using UnityEngine;
using System;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;
using TMPro;

public class Phone1Script : MonoBehaviour
{
    #if UNITY_ANDROID
    public class GyroVals
    {
        public String IPa;
        public float pitch;
        public float yaw;
        public float roll;
    }
    [SerializeField] private UDPManager typeManager;
    [SerializeField] private UDPHandler Handler;

    [SerializeField] private TCPHandler tcpHandler;
    [SerializeField] private TCPManager tcpManager;

    public ServerInfo serverInfo;
    public SelfData selfData;
    private String IP;
    private GyroVals gyr;

    [Header("Connection Status (optional)")]
    [Tooltip("Optional UI text shown while connecting / on disconnect. Safe to leave unassigned.")]
    public TMP_Text connectionStatusText;

    [Header("Gyro Send Rate")]
    [Tooltip("How many times per second gyro data is sent to the host. Lower = less bandwidth, higher = smoother remote movement.")]
    [Range(5f, 60f)] public float gyroSendRate = 20f;
    private float gyroSendTimer = 0f;

    private bool isConnected = false;

    private void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        typeManager.RegisterType<GyroVals>("gyro");

        typeManager.RegisterType<HapticFeedbackPayload>("Haptic");
        typeManager.Subscribe<HapticFeedbackPayload>("Haptic", HandleHaptic);

        // NEW: benchmark echo endpoint (this phone's UDP + TCP managers).
        BenchmarkEndpoint.RegisterOn(typeManager, tcpManager);
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(selfData.IPAddress))
        {
            IP = selfData.IPAddress;
        }
        else
        {
            Debug.LogWarning("[Phone1] selfData.IPAddress was empty; falling back to a fresh IP query. " +
                              "This may not match what the host has on record.");
            IP = Handler.GetOwnIPAddress();
        }

        gyr = new GyroVals();
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = true;
        }

        if (connectionStatusText != null)
            connectionStatusText.text = "Connecting to host...";

        tcpHandler.OnConnectedToServer += HandleConnected;
        tcpHandler.OnConnectionFailed += HandleConnectionFailed;
        tcpHandler.ConnectToServer(serverInfo.ServerIP);
    }

    private void OnDestroy()
    {
        tcpHandler.OnConnectedToServer -= HandleConnected;
        tcpHandler.OnConnectionFailed -= HandleConnectionFailed;
    }

    private void HandleConnected()
    {
        isConnected = true;
        if (connectionStatusText != null)
            connectionStatusText.text = "Connected.";
    }

    private void HandleConnectionFailed(string errorMessage)
    {
        isConnected = false;
        Debug.LogError($"[Phone1] Failed to connect to host: {errorMessage}");

        if (connectionStatusText != null)
            connectionStatusText.text = "Failed to connect to host.";
    }

    void Update()
    {
        if (!Input.gyro.enabled) return;
        if (!isConnected) return; // don't spam gyro data before the connection is confirmed

        gyroSendTimer += Time.deltaTime;
        float sendInterval = 1f / Mathf.Max(gyroSendRate, 0.01f);

        if (gyroSendTimer < sendInterval) return;
        gyroSendTimer = 0f;

        Vector3 angles = Input.gyro.gravity;
        gyr.IPa = IP;
        gyr.pitch = angles.x;
        gyr.yaw = angles.y;
        gyr.roll = angles.z;
        typeManager.SendObject("gyro", gyr, serverInfo.ServerIP);
    }

    // NEW: handles an inbound haptic event from the host. Unity's
    // Handheld.Vibrate() has no amplitude control, so intensityRatio can't
    // drive a true variable-strength buzz without a native plugin - this is
    // an approximation: skip very light hits, double-pulse strong ones.
    
    private void HandleHaptic(HapticFeedbackPayload payload)
    {
        
        if (payload.intensityRatio < 0.15f) return;

        Handheld.Vibrate();

        if (payload.intensityRatio > 0.7f)
            StartCoroutine(DoubleVibratePulse());
    }

    private System.Collections.IEnumerator DoubleVibratePulse()
    {
        yield return new WaitForSeconds(0.08f);
        Handheld.Vibrate();
    }

    #endif
    public void GoBackToHome()
    {
        SceneManager.LoadScene("Final_Lobby");
    }
}