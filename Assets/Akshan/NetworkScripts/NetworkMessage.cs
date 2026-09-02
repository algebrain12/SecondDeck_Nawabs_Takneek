using System;

// Wire envelope - matches the shape you sketched.
// 'type' tells the receiver which class to deserialize 'content' into.
[Serializable]
public class NetMessage
{
    public int ID;
    public string type;
    public byte[] content; // UTF8 JSON bytes of the actual payload object
}

public enum NetworkRole
{
    Host,
    MobileClient
}

public class NetworkClient
{
    public string IPAddress;
    public NetworkRole role;
    public string DeviceID;
    public string Nickname;

}