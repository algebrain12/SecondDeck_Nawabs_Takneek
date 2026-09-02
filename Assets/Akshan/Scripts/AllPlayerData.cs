using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerRole
{
    SoleController,
    P1,
    P2
}

public class PlayerData
{
    public String IPAddress;
    public String NickName;
    public PlayerRole playerRole;
    public Ability ability;
    public int ShipIndex;
}

[CreateAssetMenu(fileName = "AllPlayerData", menuName = "Scriptable Objects/AllPlayerData")]
public class AllPlayerData : ScriptableObject
{
    public List<PlayerData> playerList;
    public LobbyType lobbyType;
}
