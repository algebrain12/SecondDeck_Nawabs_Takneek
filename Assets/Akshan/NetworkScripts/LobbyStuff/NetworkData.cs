using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NetworkData", menuName = "Networking/NetworkData")]
public class NetworkData : ScriptableObject
{
    public NetworkClient ThisDevice;

    public List<NetworkClient> currentClients;

    public NetworkClient currentHost;
}
