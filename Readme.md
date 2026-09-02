# Second Deck

**Takneek PS – Zenith: "Second Screen: Beyond the Controller"**

**Team Members:**
- Akshan Sharma - 250087
- Sushant Prabhu - 251102
- Amardeep - 250109
- Jayesh Girase - 250485
- Arpan Mittal - 260183
- Divyanshu Lohani - 265102
- Aryan Raj - 260197

---

## Game Overview

A real-time multiplayer naval combat game. Ships battle on a shared arena, firing cannons and using unique active/passive abilities until only one remains. A PC/laptop runs the primary game (physics, combat resolution, win condition, camera), while one or more smartphones connect over the local network and act as a genuine second screen — not just a remote controller.

### How the smartphone is more than a controller

- **Haptic feedback** — the phone vibrates when *your* cannon shot actually lands a valid fire (not on every tap — only once the host confirms it), and again with a distinct pulse pattern when *your* ship takes damage. This is information the primary display has no way to convey to a specific player individually.
- **Per-player private information** — each phone shows its own ability's passive/active description, live ability cooldown, and cannon connection status: information relevant to that one player, never crowded onto the shared primary display.
- **Gyroscope steering** — ship movement is controlled by tilting the phone (pitch/yaw/roll), not a d-pad or joystick.
- **Accelerometer-based flick firing** — cannons can be fired with a physical flick of the phone (via gravity-vector delta or gravity-compensated linear acceleration, depending on scene) as an alternative to on-screen buttons.
- **Split-role multiplayer** — in two-player-per-ship mode, one phone is dedicated purely to steering (gyroscope) while a second phone is dedicated to combat (fire buttons/flick + ability), so two people genuinely share control of one ship through two different sensing modalities.

### Abilities

Every ship spawns with one randomly assigned ability, each with a distinct passive stat modifier and an active button with its own cooldown:

| Ability | Passive | Active | Cooldown |
|---|---|---|---|
| Invincible | Reduced max health (50) | Brief total invincibility (5s) | 30s |
| Healer | Weaker cannon (5 dmg) | Heal 25% of current health | 10s |
| Randy | Random cannon damage (1–10) each match | Instantly destroys a random ship — including possibly your own | 100s |
| Striker | +40% acceleration, +30% top speed | Quick forward dash | 20s |
| Sniper | +damage (12), −max health (70) | Doubles cannon damage for 6s | 25s |
| Tank | +max health (150), −acceleration | 50% damage reduction for 5s | 35s |
| Saboteur | No passive change | Slows a random enemy by 60% for 4s | 20s |
| Phantom | +30% turn speed | Instant forward teleport (15 units) | 15s |
| Berserker | +max health (130), −damage (3) | AoE cleave: 15 dmg to all enemies within 15 units | 40s |

---

## Number of Supported Smartphones

- **One-Player mode:** 1 smartphone = 1 ship. Any number of players/ships supported, limited by available spawn points.
- **Two-Player mode:** 2 smartphones = 1 ship — one dedicated to movement (gyroscope), one dedicated to combat (fire + ability). Scales the same way, in pairs.


---

## How to Run the Game

### Requirements

- **Host device:** Windows PC or laptop capable of running the Unity build, on the same Wi-Fi network as the phones.
- **Client device(s):** Android smartphone(s) with a gyroscope, accelerometer, and vibration motor. One phone must be able to host a Wi-Fi hotspot (see connection instructions below).

### Steps

1. Download and run the host application on the PC/laptop.
2. Install the Android client app (`.apk`) on each participating phone.
3. Connect all devices to the same Wi-Fi network (see below). For the windows host device, endure that the Network is set as Private, not Public. Further, allow any permissions required by the application.
4. On the PC, enter a lobby name, click **Host**.
5. On each phone, the app auto-discovers open lobbies over the local network (UDP broadcast) — select the lobby and tap join.
6. On the PC, choose **One-Player** or **Two-Player** mode once all intended phones have joined.
7. Click **Start** — the host assigns each phone a ship/role/ability and transitions everyone into the match.

---

## Instructions to Connect the Smartphone(s)

All devices must be on the **same Wi-Fi network**. This is provided by **enabling the mobile hotspot on one of the participating phones** — the PC and all other phones connect to that hotspot as their Wi-Fi network before launching the app. (A team-hosted Wi-Fi hotspot is an acceptable topology per the organizers' clarification.)

Once all devices are on that network:
1. Launch the host build on the PC.
2. Launch the client app on each phone.
3. Phones automatically discover the hosted lobby via UDP broadcast on the shared network — no manual IP entry required.
4. Tap the discovered lobby to join.

---

## Communication Architecture

Two transports run side by side, both using the same lightweight JSON envelope for message routing:

```csharp
public class NetMessage
{
    public int ID;
    public string type;     // routes to a registered handler by string key
    public byte[] content;  // UTF8 JSON bytes of the actual payload object
}
```

| Transport | Used for | Why |
|---|---|---|
| **UDP** (port 9000) | Gyro movement stream, lobby discovery broadcast, benchmark echo | High-frequency, loss-tolerant data — a dropped orientation update is imperceptible, and UDP avoids the overhead/head-of-line blocking of TCP for a continuous stream. |
| **TCP** (port 9100) | Fire/ability requests, lobby join, haptic feedback, benchmark echo | Delivery matters — a dropped "fire" or "join" request is a real gameplay bug, not a rounding error. Length-prefixed framing (4-byte prefix + UTF8 payload) over a persistent per-client connection. |

**Lobby/connection flow:**
1. Host starts a TCP listener and begins UDP-broadcasting a `LobbyPostRequest` (lobby name + host IP) on the shared network.
2. Clients listen for that broadcast and display discoverable lobbies.
3. On selecting a lobby, the client TCP-connects to the host and sends a `ClientJoinRequest`.
4. Once all players are ready, the host assigns each client a `ShipIndex`, `Ability`, and target scene via a `GameStartRequest`, sent directly to that client's IP over TCP.

**In-match:**
- Movement phones stream `GyroVals` over UDP at a configurable rate (throttled independently of frame rate).
- Combat phones send `ActionRequestClass` ("CL"/"CR"/"AB") over TCP; the host validates and resolves the action server-side.
- The host pushes `HapticFeedbackPayload` back to the relevant phone(s) over UDP on a confirmed cannon hit or on taking damage.

---

## Benchmark Communication Endpoint

Per the organizers' requirement, the benchmark endpoint is exposed on **the same communication interface used during normal gameplay**, reachable as soon as any scene's network managers are active (lobby included).

**Message type:** `"Benchmark"` (registered on both the UDP and TCP managers, host and phones).

```csharp
public class BenchmarkPingPayload
{
    public string SenderIP;   // REQUIRED - self-reported, see constraint #1 below
    public int SequenceId;    // caller's correlation id, echoed back unchanged
    public string Payload;    // arbitrary filler string to vary message/packet size
}
```

**Connection:**
- UDP: connectionless — send a `NetMessage`-wrapped `BenchmarkPingPayload` directly to `hostIP:9000` (or a phone's IP on the same port, if that phone's scene is active).
- TCP: standard `TcpClient.Connect(deviceIP, 9100)`, no join handshake required before sending pings.

**Response:** on receipt of a `"Benchmark"` message, the device immediately echoes the same `BenchmarkPingPayload` (unchanged) back to `SenderIP`, over the same transport it arrived on. The round trip **is** the acknowledgement — round-trip time, jitter, and message loss/duplication (via `SequenceId`) can all be derived from the sender's own send/receive timestamps.

**Constraints:**
1. `SenderIP` must be filled in by the caller — the receive path in this codebase does not preserve network-level sender identity by the time a message reaches a handler (this applies to every message type here, not just benchmarking). A ping with an empty `SenderIP` is logged and silently dropped.
2. UDP replies always go to the fixed configured `remotePort` (default **9000**), regardless of which port the ping was sent from — listen there.
3. TCP replies require an already-established connection; connect before sending pings.
4. The endpoint is scene-scoped, not a single persistent process — each scene (lobby, host gameplay, each phone scene) registers it independently on load. A brief gap during scene transitions is expected, and is itself valid "connection/reconnection behaviour" data if a benchmark run spans one.
5. Max TCP frame size: 10,000,000 bytes. UDP is bound by standard OS datagram limits.


---

## Third-Party Assets & Licenses

Music Track: Park Vibes by Filo Starquez
Source: https://freetouse.com/music

Music Track: Ocean Waves by u_0lndlt5pdf
Source: https://pixabay.com/sound-effects

Music Track: Epic Cinematic Explosion by Universfield
Source: https://pixabay.com/sound-effects

War FX by Jean Moreno
Source: Unity Asset Store

AllSkyFree by rpgwhitelock
Source: Unity Asset Store


## AI Tool Usage

Portions of this project's implementation were developed with AI coding assistance (Claude, Anthropic).
