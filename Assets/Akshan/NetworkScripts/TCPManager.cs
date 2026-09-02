using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class TCPManager : MonoBehaviour
{
    [SerializeField] public TCPHandler tcpManager;

    private readonly Dictionary<string, Type> typeRegistry = new Dictionary<string, Type>();

    private readonly Dictionary<string, Dictionary<Delegate, Action<object>>> subscribers
        = new Dictionary<string, Dictionary<Delegate, Action<object>>>();

    private int messageCounter = 0;

    // ---------- Registration ----------

    public void RegisterType<T>(string typeKey)
    {
        typeRegistry[typeKey] = typeof(T);
    }

    // ---------- Subscribing ----------

    public void Subscribe<T>(string typeKey, Action<T> callback)
    {
        if (!subscribers.TryGetValue(typeKey, out var dict))
        {
            dict = new Dictionary<Delegate, Action<object>>();
            subscribers[typeKey] = dict;
        }
        dict[callback] = obj => callback((T)obj);
    }

    public void Unsubscribe<T>(string typeKey, Action<T> callback)
    {
        if (subscribers.TryGetValue(typeKey, out var dict))
            dict.Remove(callback);
    }

    private void Dispatch(string typeKey, object payload)
    {
        if (!subscribers.TryGetValue(typeKey, out var dict))
        {
            Debug.LogWarning($"[NetworkTypeManagerTCP] No subscribers for '{typeKey}'.");
            return;
        }

        foreach (var wrapper in dict.Values)
        {
            try { wrapper(payload); }
            catch (Exception e) { Debug.LogError($"[NetworkTypeManagerTCP] Handler error for '{typeKey}': {e}"); }
        }
    }

    // ---------- Sending ----------

    public NetMessage Pack<T>(string typeKey, T payload)
    {
        if (!typeRegistry.TryGetValue(typeKey, out Type registered))
        {
            Debug.LogError($"[NetworkTypeManagerTCP] '{typeKey}' not registered. Call RegisterType<T>() first.");
            return null;
        }
        if (registered != typeof(T))
        {
            Debug.LogError($"[NetworkTypeManagerTCP] '{typeKey}' registered as {registered.Name}, got {typeof(T).Name}.");
            return null;
        }

        byte[] contentBytes;

        if (typeof(T) == typeof(string))
        {
            contentBytes = Encoding.UTF8.GetBytes((string)(object)payload);
        }
        else
        {
            string json = JsonUtility.ToJson(payload);
            contentBytes = Encoding.UTF8.GetBytes(json);
        }

        return new NetMessage
        {
            ID = messageCounter++,
            type = typeKey,
            content = contentBytes
        };
    }

    // Sends an object of type T, tagged with typeKey.
    // ip == null: server broadcasts to all connected clients, client sends to its server.
    // ip != null: server sends to that specific client (ignored on client mode).
    public void SendObject<T>(string typeKey, T payload, string ip = null)
    {
        NetMessage msg = Pack(typeKey, payload);
        if (msg == null) return;

        string wire = JsonUtility.ToJson(msg);

        if (string.IsNullOrEmpty(ip))
            tcpManager.SendTCP(wire);
        else
            tcpManager.SendTCP2(wire, ip);
    }

    public void HandleRaw(string raw)
    {
        NetMessage netMsg;
        try { netMsg = JsonUtility.FromJson<NetMessage>(raw); }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkTypeManagerTCP] Bad envelope: {e.Message}");
            return;
        }

        if (netMsg == null || string.IsNullOrEmpty(netMsg.type))
        {
            Debug.LogWarning("[NetworkTypeManagerTCP] Malformed message.");
            return;
        }

        if (!typeRegistry.TryGetValue(netMsg.type, out Type targetType))
        {
            Debug.LogWarning($"[NetworkTypeManagerTCP] Unknown type '{netMsg.type}' - nothing registered.");
            return;
        }

        object payload;
        try
        {
            if (targetType == typeof(string))
            {
                payload = Encoding.UTF8.GetString(netMsg.content);
            }
            else
            {
                string payloadJson = Encoding.UTF8.GetString(netMsg.content);
                payload = JsonUtility.FromJson(payloadJson, targetType);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkTypeManagerTCP] Failed to deserialize '{netMsg.type}' as {targetType}: {e.Message}");
            return;
        }

        Dispatch(netMsg.type, payload);
    }
}