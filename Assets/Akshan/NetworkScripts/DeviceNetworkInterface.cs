using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class DeviceNetworkInterface : MonoBehaviour
{
    [SerializeField] private UDPManager typeManager;
    [SerializeField] private UDPHandler Handler;

    [SerializeField] private TCPHandler tcpHandler;

    private List<GameObject> PlayerObjects;


    public TextMeshProUGUI txt;
    public class GyroVals
    {
        public String IPa;
        public float pitch;
        public float yaw;
        public float roll;
    }

    public GyroVals gyr;
    public GameObject Playerss;
    public AllPlayerData data;
    public ServerInfo serverInfo;


    
    private void Awake()
    {
        #if !(UNITY_ANDROID && !UNITY_EDITOR)
            tcpHandler.StartAsServer();
        #endif
        typeManager.RegisterType<String>("text-message");
        typeManager.Subscribe<String>("text-message", GetItDone);

        typeManager.RegisterType<GyroVals>("gyro");
        typeManager.Subscribe<GyroVals>("gyro", MovePlayer);
    }

    public void SendM()
    {
        if(txt.text == "hello")typeManager.SendObject("text-message", "new");
        else typeManager.SendObject("text-message", "hello");
    }

    void Start()
    {
        PlayerObjects = new List<GameObject>();
        #if UNITY_ANDROID && !UNITY_EDITOR
        tcpHandler.ConnectToServer(serverInfo.ServerIP);
        gyr = new GyroVals();
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = true;
        }
        return;
        #endif
        float minX = 1744f, maxX = 1800f;
        float minZ = 1937f, maxZ = 1980f;
        float fixedY = 73.04f; // Set this to your ground/spawn height

        foreach (PlayerData player in data.playerList)
        {

            float randomX = UnityEngine.Random.Range(minX, maxX);
            float randomZ = UnityEngine.Random.Range(minZ, maxZ);
            Vector3 spawnPosition = new Vector3(randomX, fixedY, randomZ);

            
            GameObject newPlayer = Instantiate(Playerss, spawnPosition, Quaternion.identity);
            PlayerObjects.Add(newPlayer);
        }
    }

    void MovePlayer(GyroVals gg)
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
            txt.text = "Pitch: " + gg.pitch.ToString() + "\nRoll: "+gg.roll.ToString()+"\nYaw:"+ gg.yaw.ToString();
            return;
        #endif
        Debug.Log(gg.yaw);
        foreach(PlayerData player in data.playerList)
        {
            if(player.IPAddress == gg.IPa)
            {
                PlayerObjects[player.ShipIndex].GetComponent<BoatCont>().MoveSS(gg.pitch, gg.yaw, gg.yaw);
            }
        }

    }

    void GetItDone(String str)
    {
        txt.text = str;
    }

    // Update is called once per frame
    void Update()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
            if (!Input.gyro.enabled) return;

            Vector3 angles = Input.gyro.gravity;
            gyr.IPa = Handler.GetOwnIPAddress();
            gyr.pitch = angles.x;
            gyr.yaw = angles.y;
            gyr.roll = angles.z;
            typeManager.SendObject("gyro", gyr, serverInfo.ServerIP);

        #endif
    }

    private Quaternion ConvertGyroRotation(Quaternion q)
    {
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }
}
