using System;
using TMPro;
using UnityEngine;

public class TCPtester : MonoBehaviour
{
    public TCPHandler tch;
    public TCPManager tmg;

    public TMP_Text textBox;

    void Awake()
    {
        #if !(UNITY_ANDROID && !UNITY_EDITOR)
            tch.StartAsServer();
        #endif
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
            tch.ConnectToServer("10.203.162.59");
        #endif
        tmg.RegisterType<String>("msg");
        tmg.Subscribe<String>("msg", ChangeMessage);
    }

    public void ChangeMessage(String a)
    {
        textBox.text = a;
    }

    public void SendMessage()
    {
        tmg.SendObject<String>("msg", "hello");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
