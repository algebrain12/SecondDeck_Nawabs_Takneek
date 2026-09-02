using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class UDPManager : MonoBehaviour
{
    [SerializeField] public UDPHandler udpManager;

    // type-key -> the C# Type it deserializes to
    private readonly Dictionary<string, Type> typeRegistry = new Dictionary<string, Type>();

    // type-key -> (original delegate -> wrapped invoker), so Unsubscribe works properly
    private readonly Dictionary<string, Dictionary<Delegate, Action<object>>> subscribers
        = new Dictionary<string, Dictionary<Delegate, Action<object>>>();

    private int messageCounter = 0;

    // ---------- Registration ----------

    // Associate a type-key ("PlayerMove", "Chat", "Handshake", ...) with a class.
    // T must be marked [Serializable] with public fields (JsonUtility rules).
    public void RegisterType<T>(string typeKey)
    {
        typeRegistry[typeKey] = typeof(T);
    }

    // ---------- Subscribing to incoming messages ----------

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
            Debug.LogWarning($"[NetworkTypeManager] No subscribers for '{typeKey}'.");
            return;
        }

        foreach (var wrapper in dict.Values)
        {
            try { wrapper(payload); }
            catch (Exception e) { Debug.LogError($"[NetworkTypeManager] Handler error for '{typeKey}': {e}"); }
        }
    }

    // ---------- Sending ----------

    // Packs payload into a NetMessage, ready to be turned into a wire string.
    public NetMessage Pack<T>(string typeKey, T payload)
    {
        if (!typeRegistry.TryGetValue(typeKey, out Type registered))
        {
            Debug.LogError($"[NetworkTypeManager] '{typeKey}' not registered. Call RegisterType<T>() first.");
            return null;
        }
        if (registered != typeof(T))
        {
            Debug.LogError($"[NetworkTypeManager] '{typeKey}' registered as {registered.Name}, got {typeof(T).Name}.");
            return null;
        }

        byte[] contentBytes;

        if (typeof(T) == typeof(string))
        {
            // JsonUtility can't serialize bare strings - pass them through directly.
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

    // Sends an object of type T, tagged with typeKey, to a specific IP (or broadcast if ip is null/empty).
    public void SendObject<T>(string typeKey, T payload, string ip = null)
    {
        NetMessage msg = Pack(typeKey, payload);
        if (msg == null) return;

        string wire = JsonUtility.ToJson(msg);

        if (string.IsNullOrEmpty(ip))
            udpManager.SendUDP(wire);      // uses UDPManager's current remoteIP (broadcast)
        else
            udpManager.SendUDP2(wire, ip); // direct to a known peer
    }

    public void HandleRaw(string raw)
    {
        NetMessage netMsg;
        try { netMsg = JsonUtility.FromJson<NetMessage>(raw); }
        catch (Exception e)
        {
            Debug.LogWarning($"[NetworkTypeManager] Bad envelope: {e.Message}");
            return;
        }

        if (netMsg == null || string.IsNullOrEmpty(netMsg.type))
        {
            Debug.LogWarning("[NetworkTypeManager] Malformed message.");
            return;
        }

        if (!typeRegistry.TryGetValue(netMsg.type, out Type targetType))
        {
            Debug.LogWarning($"[NetworkTypeManager] Unknown type '{netMsg.type}' - nothing registered.");
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
            Debug.LogError($"[NetworkTypeManager] Failed to deserialize '{netMsg.type}' as {targetType}: {e.Message}");
            return;
        }

        Dispatch(netMsg.type, payload);
    }
}
