using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using UnityEngine.UI;

public class Phone2Script : MonoBehaviour
{
    #if UNITY_ANDROID
    [SerializeField] private UDPManager typeManager;
    [SerializeField] private UDPHandler Handler;

    [SerializeField] private TCPHandler tcpHandler;
    [SerializeField] private TCPManager tcpManager;

    public ServerInfo serverInfo;
    public SelfData selfData;
    public TMP_Text abilityText;

    [Header("Ability Info")]
    [Tooltip("Optional: assign to show what the current ability's passive and active effects actually do.")]
    public TMP_Text abilityDescriptionText;

    public Button AbilityButton;
    private float AbilityCooldownLeft;
    
    public GameObject abilityTextObject;
    public TMP_Text CoolDownText;

    [Header("Fire Buttons (optional, for connection-gating)")]
    [Tooltip("Optional: assign so Fire buttons auto-disable until connected to the host.")]
    public Button FireLeftButton;
    public Button FireRightButton;

    [Header("Connection Status (optional)")]
    [Tooltip("Optional UI text shown while connecting / on disconnect. Safe to leave unassigned.")]
    public TMP_Text connectionStatusText;

    // CHANGED: swapped from gyro-angle-delta detection to the same
    // accelerometer-based approach as CombinedSceneScript - simpler, and
    // responds to an actual physical translation of the phone rather than
    // a change in tilt/orientation.
    [Header("Flick Detection (Accelerometer-based cannon fire)")]
    [Tooltip("Minimum sideways linear acceleration (in g's, ~9.81 m/s^2) to count as a flick. " +
             "Uses Input.gyro.userAcceleration, which is gravity-compensated, so it responds to " +
             "a quick sideways translation of the phone rather than a change in tilt/orientation.")]
    [SerializeField] private float flickAccelThreshold = 0.8f;
    [Tooltip("Minimum seconds between consecutive flick-triggered fires.")]
    [SerializeField] private float flickCooldown = 1f;

    private String IP;

    // REMOVED: previousGravity - no longer needed, the accelerometer
    // approach reads a single instantaneous sample rather than comparing
    // against a previous frame's gravity vector.
    private float lastFlickTime = -1f;
    private bool gyroInitialized = false;

    void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        tcpManager.RegisterType<ActionRequestClass>("Request");

        typeManager.RegisterType<HapticFeedbackPayload>("Haptic");
        typeManager.Subscribe<HapticFeedbackPayload>("Haptic", HandleHaptic);

        BenchmarkEndpoint.RegisterOn(typeManager, tcpManager);

        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = true;
            gyroInitialized = true;
            Debug.Log("[Phone2] Gyroscope enabled for flick detection.");
        }
        else
        {
            Debug.LogWarning("[Phone2] Gyroscope not supported on this device.");
            gyroInitialized = false;
        }
    }
    
    
    void Start()
    {
        if (!string.IsNullOrEmpty(selfData.IPAddress))
        {
            IP = selfData.IPAddress;
        }
        else
        {
            Debug.LogWarning("[Phone2] selfData.IPAddress was empty; falling back to a fresh IP query. " +
                              "This may not match what the host has on record.");
            IP = Handler.GetOwnIPAddress();
        }

        abilityText.text = selfData.ability.ToString();

        if (abilityDescriptionText != null)
        {
            ability info = Abilities.Get(selfData.ability);
            abilityDescriptionText.text = $"Passive: {info.PassiveDescription}\nActive: {info.ActiveDescription}";
        }

        AbilityCooldownLeft = Abilities.Get(selfData.ability).CoolDown;

        if (AbilityCooldownLeft > 0f)
        {
            AbilityButton.interactable = false;
            abilityTextObject.SetActive(true);
        }

        SetActionButtonsInteractable(false);
        if (connectionStatusText != null)
            connectionStatusText.text = "Connecting to host...";

        tcpHandler.OnConnectedToServer += HandleConnected;
        tcpHandler.OnConnectionFailed += HandleConnectionFailed;
        tcpHandler.ConnectToServer(serverInfo.ServerIP);

        // REMOVED: the previousGravity initialization block - not needed
        // by the accelerometer-based detector.
    }

    private void OnDestroy()
    {
        tcpHandler.OnConnectedToServer -= HandleConnected;
        tcpHandler.OnConnectionFailed -= HandleConnectionFailed;
    }

    private void HandleConnected()
    {
        if (connectionStatusText != null)
            connectionStatusText.text = "Connected.";

        SetActionButtonsInteractable(true);

        if (AbilityCooldownLeft <= 0f)
            AbilityButton.interactable = true;
    }

    private void HandleConnectionFailed(string errorMessage)
    {
        Debug.LogError($"[Phone2] Failed to connect to host: {errorMessage}");

        if (connectionStatusText != null)
            connectionStatusText.text = "Failed to connect to host.";

        SetActionButtonsInteractable(false);
        AbilityButton.interactable = false;
    }

    private void SetActionButtonsInteractable(bool interactable)
    {
        if (FireLeftButton != null) FireLeftButton.interactable = interactable;
        if (FireRightButton != null) FireRightButton.interactable = interactable;
    }

    public void FireLeftB()
    {
        if (!tcpHandler.IsConnected)
        {
            Debug.LogWarning("[Phone2] FireLeftB ignored: not connected to host.");
            return;
        }

        ActionRequestClass cls = new ActionRequestClass();
        cls.ActionName = "CL";
        cls.IPAdd = IP;
        tcpManager.SendObject<ActionRequestClass>("Request", cls, serverInfo.ServerIP);
    }

    public void FireRightB()
    {
        if (!tcpHandler.IsConnected)
        {
            Debug.LogWarning("[Phone2] FireRightB ignored: not connected to host.");
            return;
        }

        ActionRequestClass cls = new ActionRequestClass();
        cls.ActionName = "CR";
        cls.IPAdd = IP;
        tcpManager.SendObject<ActionRequestClass>("Request", cls, serverInfo.ServerIP);
    }

    public void PressedAbility()
    {
        if (!tcpHandler.IsConnected)
        {
            Debug.LogWarning("[Phone2] PressedAbility ignored: not connected to host.");
            return;
        }

        ActionRequestClass cls = new ActionRequestClass();
        cls.ActionName = "AB";
        cls.IPAdd = IP;
        tcpManager.SendObject<ActionRequestClass>("Request", cls, serverInfo.ServerIP);
        AbilityCooldownLeft = Abilities.Get(selfData.ability).CoolDown;
        AbilityButton.interactable = false;
    }

    public void BackToLobby()
    {
        SceneManager.LoadScene("Final_Lobby");
    }

    void Update()
    {
        // Handle ability cooldown display
        if(AbilityCooldownLeft < 0 && AbilityButton.interactable == false)
        {
            AbilityButton.interactable = true;
            abilityTextObject.SetActive(false);
        }
        else
        {
            if(AbilityCooldownLeft > 0)AbilityCooldownLeft -= Time.deltaTime;
            if(abilityTextObject.activeSelf == false && AbilityCooldownLeft > 0)abilityTextObject.SetActive(true);
            if(abilityTextObject.activeSelf)CoolDownText.text = ((int)AbilityCooldownLeft).ToString();
        }
        
        // Detect flick via accelerometer
        DetectFlick();
    }

    // CHANGED: replaced the gyro-gravity-angle-delta algorithm with
    // CombinedSceneScript's accelerometer-threshold approach.
    private void DetectFlick()
    {
        if (!gyroInitialized)
            return;

        if (Time.time - lastFlickTime < flickCooldown)
            return;

        float sidewaysAccel = Input.gyro.userAcceleration.x;

        if (sidewaysAccel > flickAccelThreshold)
        {
            Debug.Log($"[Phone2] Left flick detected! Accel: {sidewaysAccel:F2}g");
            FireLeftB();
            lastFlickTime = Time.time;
        }
        else if (sidewaysAccel < -flickAccelThreshold)
        {
            Debug.Log($"[Phone2] Right flick detected! Accel: {sidewaysAccel:F2}g");
            FireRightB();
            lastFlickTime = Time.time;
        }
    }

    // Handles an inbound haptic event from the host.
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
}