using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SelfData", menuName = "Scriptable Objects/SelfData")]
public class SelfData : ScriptableObject
{
    public String NickName;
    public String IPAddress;
    public PlayerRole playerRole;

    public Ability ability;
}
