using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ServerInfo", menuName = "Scriptable Objects/ServerInfo")]
public class ServerInfo : ScriptableObject
{
    public String ServerIP;
    public String LobbyName;
}
