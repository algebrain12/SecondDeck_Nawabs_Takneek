using System;
using UnityEngine;

// The transport layer (UDPHandler/TCPHandler) discards sender identity before
// a message reaches a Subscribe() callback - see the note in chat. So this
// payload self-reports the sender's own IP, same pattern as
// ActionRequestClass.IPAdd / GyroVals.IPa elsewhere in this codebase.
[Serializable]
public class BenchmarkPingPayload
{
    public string SenderIP;   // REQUIRED - the address to echo back to (UDP) / used to
                               // look up the live connection (TCP). Echo does nothing
                               // if this is empty.
    public int SequenceId;    // Caller's own correlation id - echoed back unchanged.
    public string Payload;    // Arbitrary filler string - lets the organizer vary
                               // message/packet size (PS point 3). Echoed back unchanged.
}

// Registers a "Benchmark" message type on a given UDPManager/TCPManager and
// echoes any received BenchmarkPingPayload straight back to SenderIP,
// unchanged, over whichever transport it arrived on. Call RegisterOn() once
// per scene, from that scene's own Awake()/Start() - see GameHandler,
// LobbyScript, Phone1Script, Phone2Script, CombinedSceneScript.
public static class BenchmarkEndpoint
{
    public static void RegisterOn(UDPManager udp, TCPManager tcp)
    {
        if (udp != null)
        {
            udp.RegisterType<BenchmarkPingPayload>("Benchmark");
            udp.Subscribe<BenchmarkPingPayload>("Benchmark", ping => EchoUdp(udp, ping));
        }

        if (tcp != null)
        {
            tcp.RegisterType<BenchmarkPingPayload>("Benchmark");
            tcp.Subscribe<BenchmarkPingPayload>("Benchmark", ping => EchoTcp(tcp, ping));
        }
    }

    private static void EchoUdp(UDPManager udp, BenchmarkPingPayload ping)
    {
        if (string.IsNullOrEmpty(ping.SenderIP))
        {
            Debug.LogWarning("[Benchmark] UDP ping missing SenderIP - cannot echo back. See BENCHMARK_ENDPOINT.md.");
            return;
        }

        udp.SendObject("Benchmark", ping, ping.SenderIP);
    }

    private static void EchoTcp(TCPManager tcp, BenchmarkPingPayload ping)
    {
        if (string.IsNullOrEmpty(ping.SenderIP))
        {
            Debug.LogWarning("[Benchmark] TCP ping missing SenderIP - cannot echo back. See BENCHMARK_ENDPOINT.md.");
            return;
        }

        // Requires the sender to already be a live TCP connection (i.e. it
        // actually called TcpClient.Connect() to this host beforehand) -
        // SendTCP2 looks it up in TCPHandler's connectedClients by IP.
        tcp.SendObject("Benchmark", ping, ping.SenderIP);
    }
}