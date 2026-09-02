using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameHandler : MonoBehaviour
{
    [SerializeField] private UDPManager typeManager;
    [SerializeField] private UDPHandler Handler;

    [SerializeField] private TCPHandler tcpHandler;
    [SerializeField] private TCPManager tcpManager;

    [SerializeField] private SpawnAreaDisplayer spawnArea;

    public GameObject Camera;
    private UnityEngine.Camera cam; // cached Camera component

    [Header("Camera Framing")]
    [Tooltip("Extra world-space padding added around the bounding box of all ships.")]
    public float framingPadding = 100f;

    [Tooltip("Minimum distance the camera is allowed to sit from the ships' center.")]
    public float minCameraDistance = 50f;

    [Tooltip("Maximum distance the camera is allowed to sit from the ships' center.")]
    public float maxCameraDistance = 250f;

    [Tooltip("How quickly the camera position eases toward the target framing position.")]
    public float framingSmoothSpeed = 4f;

    [Header("Win Condition")]
    [Tooltip("Enabled when the game ends (only one ship - or zero, on a simultaneous death - remains).")]
    public GameObject winUIObject;

    // Prevents the win check/UI/freeze from firing more than once, and
    // stops any further input from doing anything after the game ends.
    private bool gameEnded = false;

    [Header("Ability Tuning")]
    [Tooltip("Radius (world units) for Berserker's Cleave active ability.")]
    public float berserkerCleaveRadius = 15f;
    [Tooltip("Damage dealt to each enemy within range by Berserker's Cleave.")]
    public float berserkerCleaveDamage = 15f;

    [Tooltip("Distance (world units) Phantom's Blink teleports forward.")]
    public float phantomBlinkDistance = 45f;

    [Tooltip("Cannon damage multiplier during Sniper's Focus Shot.")]
    public float sniperFocusMultiplier = 2f;
    [Tooltip("Duration (seconds) of Sniper's Focus Shot buff.")]
    public float sniperFocusDuration = 8f;

    [Tooltip("Damage reduction percent (0-1) from Tank's Shield.")]
    public float tankShieldReduction = 0.5f;
    [Tooltip("Duration (seconds) of Tank's Shield.")]
    public float tankShieldDuration = 5f;

    [Tooltip("Speed multiplier applied to a sabotaged enemy (< 1 = slower).")]
    public float saboteurSlowMultiplier = 0.4f;
    [Tooltip("Duration (seconds) of Saboteur's slow.")]
    public float saboteurSlowDuration = 4f;

    [Header("Haptics")]
    [Tooltip("intensityRatio sent for a successful cannon fire (damage haptics use the real damage ratio instead).")]
    public float cannonFireHapticIntensity = 0.35f;

    public class GyroVals
    {
        public String IPa;
        public float pitch;
        public float yaw;
        public float roll;
    }

    public GyroVals gyr;
    public GameObject PlayerPrefab;
    public AllPlayerData data;
    public ServerInfo serverInfo;
    private List<GameObject> PlayerObjects;

    // O(1) IP -> PlayerData lookup instead of scanning data.playerList
    // on every single network message (gyro updates arrive frequently).
    private Dictionary<string, PlayerData> playerByIP;

    // NEW: reverse lookup so haptic events (damage, cannon fire) know which
    // phone IP(s) control a given ship. Built the same way playerByIP is -
    // by trusting PlayerData.ShipIndex, the same invariant RequestHandler
    // and MovePlayer already rely on for input routing.
    private Dictionary<GameObject, List<string>> shipToIPs;

    public TMP_Text WinnerNameText;

    void Awake()
    {
        if (!tcpHandler.IsServer)
            tcpHandler.StartAsServer();

        typeManager.RegisterType<GyroVals>("gyro");
        typeManager.Subscribe<GyroVals>("gyro", MovePlayer);
        tcpManager.RegisterType<ActionRequestClass>("Request");
        tcpManager.Subscribe<ActionRequestClass>("Request", RequestHandler);

        typeManager.RegisterType<HapticFeedbackPayload>("Haptic");

        // NEW: always-on benchmark echo endpoint (host side). Reachable as
        // soon as this scene's TCP/UDP managers are alive - independent of
        // match state, per the Discord Q&A clarification.
        BenchmarkEndpoint.RegisterOn(typeManager, tcpManager);

        if (Camera != null)
            cam = Camera.GetComponent<UnityEngine.Camera>();

        if (winUIObject != null)
            winUIObject.SetActive(false);
    }

    void Update()
    {
        FrameAllShips();
    }

    /// <summary>
    /// Moves the camera (position only) so that every alive ship stays inside
    /// its view frustum, with framingPadding of breathing room around them.
    /// Rotation is never touched — only the position changes, sliding back
    /// along the camera's current forward axis (and re-centering on the
    /// ships) as needed.
    /// </summary>
    void FrameAllShips()
    {
        if (cam == null || PlayerObjects == null || PlayerObjects.Count == 0)
            return;

        bool hasBounds = false;
        Bounds b = new Bounds();

        foreach (var ship in PlayerObjects)
        {
            if (ship == null) continue; // skip destroyed/sunk ships

            if (!hasBounds)
            {
                b = new Bounds(ship.transform.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(ship.transform.position);
            }
        }

        if (!hasBounds) return; // no living ships left, keep camera where it is

        b.Expand(framingPadding);

        Vector3 center = b.center;
        Vector3 extents = b.extents;

        Transform camT = cam.transform;
        Vector3 forward = camT.forward;
        Vector3 right = camT.right;
        Vector3 up = camT.up;

        float maxRight = 0f;
        float maxUp = 0f;

        for (int xi = -1; xi <= 1; xi += 2)
        for (int yi = -1; yi <= 1; yi += 2)
        for (int zi = -1; zi <= 1; zi += 2)
        {
            Vector3 corner = center + new Vector3(extents.x * xi, extents.y * yi, extents.z * zi);
            Vector3 offset = corner - center;

            float rightExtent = Mathf.Abs(Vector3.Dot(offset, right));
            float upExtent = Mathf.Abs(Vector3.Dot(offset, up));

            if (rightExtent > maxRight) maxRight = rightExtent;
            if (upExtent > maxUp) maxUp = upExtent;
        }

        float requiredDistance;

        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Max(maxUp, maxRight / cam.aspect);
            requiredDistance = Vector3.Distance(camT.position, center);
        }
        else
        {
            float halfVFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distanceForHeight = maxUp / Mathf.Tan(halfVFov);
            float distanceForWidth = maxRight / (Mathf.Tan(halfVFov) * cam.aspect);

            requiredDistance = Mathf.Max(distanceForHeight, distanceForWidth);
            requiredDistance = Mathf.Clamp(requiredDistance, minCameraDistance, maxCameraDistance);
        }

        Vector3 desiredPosition = center - forward * requiredDistance;

        // Frame-rate-independent exponential smoothing.
        float t = 1f - Mathf.Exp(-framingSmoothSpeed * Time.deltaTime);
        camT.position = Vector3.Lerp(camT.position, desiredPosition, t);
    }

    // NEW: builds the list of phone IPs whose PlayerData.ShipIndex matches
    // this ship's spawn index. Same trust relationship RequestHandler and
    // MovePlayer already depend on, just read in reverse.
    private List<string> GetIPsForShipIndex(int shipIndex)
    {
        List<string> ips = new List<string>();
        foreach (var kvp in playerByIP)
        {
            if (kvp.Value != null && kvp.Value.ShipIndex == shipIndex)
                ips.Add(kvp.Key);
        }
        return ips;
    }

    // NEW: sends a HapticFeedbackPayload to every phone IP mapped to this
    // ship (both phones, for a TwoPlayer ship). No-ops quietly if the ship
    // isn't in shipToIPs (e.g. a null-safety edge case) or has no known IPs.
    private void SendHaptic(GameObject ship, float damageDealt, bool isDamageEvent, float intensityRatio)
    {
        if (ship == null || shipToIPs == null) return;
        if (!shipToIPs.TryGetValue(ship, out List<string> ips) || ips.Count == 0) return;

        HapticFeedbackPayload payload = new HapticFeedbackPayload
        {
            damageDealt = damageDealt,
            intensityRatio = Mathf.Clamp01(intensityRatio),
            isDamageEvent = isDamageEvent
        };

        foreach (string ip in ips)
        {
            typeManager.SendObject("Haptic", payload, ip);
        }
    }

    // NEW: subscribes this ship's BoatHealth.OnDamageTaken so every damage
    // source (cannonball hits, Berserker cleave, Randy's instakill - anything
    // that calls TakeDamage) automatically triggers a haptic to that ship's
    // controlling phone(s), with no per-ability wiring needed.
    private void RegisterDamageHapticListener(GameObject ship)
    {
        if (ship == null) return;

        BoatHealth health = ship.GetComponent<BoatHealth>();
        if (health != null)
        {
            health.OnDamageTaken += (amount, ratio) => SendHaptic(ship, amount, true, ratio);
        }
    }

    void RequestHandler(ActionRequestClass request)
    {
        // Once the game has ended, ignore all further input - stops
        // late-arriving packets from touching frozen/disabled ships.
        if (gameEnded) return;

        if (playerByIP == null || !playerByIP.TryGetValue(request.IPAdd, out PlayerData player))
        {
            Debug.LogWarning($"[GameHandler] Request from unknown IP '{request.IPAdd}' ignored.");
            return;
        }

        if (player.ShipIndex < 0 || player.ShipIndex >= PlayerObjects.Count || PlayerObjects[player.ShipIndex] == null)
        {
            Debug.LogWarning($"[GameHandler] Request for invalid/destroyed ShipIndex {player.ShipIndex} ignored (ship likely sunk).");
            return;
        }

        GameObject ship = PlayerObjects[player.ShipIndex];

        BoatHealth shipHealth = ship.GetComponent<BoatHealth>();
        if (shipHealth != null && shipHealth.IsDead)
        {
            Debug.LogWarning($"[GameHandler] Request from dead ship (ShipIndex {player.ShipIndex}) ignored.");
            return;
        }

        if (request.ActionName == "CL")
        {
            // CHANGED: FireLeft() now returns bool. Only haptic-buzz the
            // requesting ship's phone(s) if the shot actually launched -
            // a cooldown-rejected request stays silent, as intended.
            bool fired = ship.GetComponent<CanonWork>().FireLeft();
            if (fired)
                SendHaptic(ship, 0f, false, cannonFireHapticIntensity);
        }
        else if (request.ActionName == "CR")
        {
            bool fired = ship.GetComponent<CanonWork>().FireRight();
            if (fired)
                SendHaptic(ship, 0f, false, cannonFireHapticIntensity);
        }
        else if (request.ActionName == "AB")
        {
            Ability ab = player.ability;
            Debug.Log(ab.ToString());
            Debug.Log(player.ShipIndex);

            BoatHealth boatHealth = shipHealth;

            if (ab == Ability.Healer)
            {
                boatHealth.currentHealth *= 1.25f;
                if (boatHealth.currentHealth > boatHealth.maxHealth)
                    boatHealth.currentHealth = boatHealth.maxHealth;
            }
            else if (ab == Ability.Invicible)
            {
                boatHealth.BecomeInvincible();
            }
            else if (ab == Ability.Randy)
            {
                if (PlayerObjects.Count > 0)
                {
                    int targetIndex = UnityEngine.Random.Range(0, PlayerObjects.Count);
                    GameObject target = PlayerObjects[targetIndex];
                    if (target != null)
                    {
                        BoatHealth targetHealth = target.GetComponent<BoatHealth>();
                        if (targetHealth != null && !targetHealth.IsDead)
                            targetHealth.TakeDamage(999999f);
                    }
                }
            }
            else if (ab == Ability.Striker)
            {
                BoatCont boatCont = ship.GetComponent<BoatCont>();
                if (boatCont != null)
                    boatCont.PerformDash();
            }
            else if (ab == Ability.Sniper)
            {
                CanonWork canon = ship.GetComponent<CanonWork>();
                if (canon != null)
                    StartCoroutine(TemporaryCanonBuff(canon, sniperFocusMultiplier, sniperFocusDuration));
            }
            else if (ab == Ability.Tank)
            {
                boatHealth.ActivateDamageReduction(tankShieldReduction, tankShieldDuration);
            }
            else if (ab == Ability.Saboteur)
            {
                List<GameObject> others = new List<GameObject>();
                foreach (var other in PlayerObjects)
                {
                    if (other != null && other != ship) others.Add(other);
                }

                if (others.Count > 0)
                {
                    GameObject target = others[UnityEngine.Random.Range(0, others.Count)];
                    BoatCont targetCont = target.GetComponent<BoatCont>();
                    if (targetCont != null)
                        targetCont.ApplyTemporarySpeedMultiplier(saboteurSlowMultiplier, saboteurSlowDuration);
                }
            }
            else if (ab == Ability.Phantom)
            {
                BoatCont boatCont = ship.GetComponent<BoatCont>();
                if (boatCont != null)
                    boatCont.Blink(phantomBlinkDistance);
            }
            else if (ab == Ability.Berserker)
            {
                foreach (var other in PlayerObjects)
                {
                    if (other == null || other == ship) continue;

                    BoatHealth otherHealth = other.GetComponent<BoatHealth>();
                    if (otherHealth == null || otherHealth.IsDead) continue;

                    if (Vector3.Distance(ship.transform.position, other.transform.position) <= berserkerCleaveRadius)
                        otherHealth.TakeDamage(berserkerCleaveDamage);
                }
            }
        }
    }

    // Temporarily multiplies a ship's cannon damage, then reverts it after
    // duration seconds. Used by Sniper's Focus Shot. Stores the value
    // present when the coroutine starts, so it correctly restores even if
    // CanonDamage was itself a non-default passive value (e.g. Berserker's
    // weakened cannon, if that combination ever mattered).
    private IEnumerator TemporaryCanonBuff(CanonWork canon, float multiplier, float duration)
    {
        float original = canon.CanonDamage;
        canon.CanonDamage = original * multiplier;

        yield return new WaitForSeconds(duration);

        if (canon != null)
            canon.CanonDamage = original;
    }
    public AudioSource audioSource;

    void MovePlayer(GyroVals gg)
    {
        if (gameEnded) return;

        Debug.Log("Party");

        if (playerByIP == null || !playerByIP.TryGetValue(gg.IPa, out PlayerData player))
            return;

        if (player.ShipIndex < 0 || player.ShipIndex >= PlayerObjects.Count || PlayerObjects[player.ShipIndex] == null)
            return;

        GameObject ship = PlayerObjects[player.ShipIndex];
        BoatHealth shipHealth = ship.GetComponent<BoatHealth>();
        if (shipHealth != null && shipHealth.IsDead)
            return;

        ship.GetComponent<BoatCont>().MoveSS(gg.pitch, gg.yaw, gg.roll);
    }

    public void BackToLobby()
    {
        SceneManager.LoadScene("Final_Lobby");
    }

    void Start()
    {
        PlayerObjects = new List<GameObject>();

        int k = data.playerList.Count;
        if (data.lobbyType == LobbyType.TwoPlayer)
        {
            k = k / 2 + k % 2;
        }

        Debug.Log(k);
        Debug.Log(data.playerList.Count);
        Debug.Log(data.lobbyType);

        playerByIP = new Dictionary<string, PlayerData>();
        shipToIPs = new Dictionary<GameObject, List<string>>();
        foreach (PlayerData p in data.playerList)
        {
            if (!string.IsNullOrEmpty(p.IPAddress))
                playerByIP[p.IPAddress] = p;
        }

        if (spawnArea == null)
        {
            Debug.LogError("[GameHandler] No SpawnAreaDisplayer assigned! Falling back is not possible.");
            return;
        }

        spawnArea.pointCount = k;
        spawnArea.GeneratePoints();

        if (spawnArea.generatedPoints.Count < k)
        {
            Debug.LogWarning($"[GameHandler] Only got {spawnArea.generatedPoints.Count}/{k} spawn points. " +
                              "Some ships may spawn at fallback/default positions.");
        }

        for (int i = 0; i < k; i++)
        {
            Vector3 spawnPosition = i < spawnArea.generatedPoints.Count
                ? spawnArea.generatedPoints[i]
                : spawnArea.transform.position;

            int f = i;
            spawnPosition.y = 73f;
            if (data.lobbyType == LobbyType.TwoPlayer) f = i * 2 + 1;

            if (f >= data.playerList.Count)
            {
                Debug.LogWarning($"[GameHandler] Player index {f} out of range for ship {i}; skipping passive/name setup.");
                GameObject fallbackShip = Instantiate(PlayerPrefab, spawnPosition, Quaternion.identity);
                PlayerObjects.Add(fallbackShip);
                RegisterWinConditionListener(fallbackShip);
                fallbackShip.GetComponent<BoatHealth>().audioSource = audioSource;

                // NEW
                shipToIPs[fallbackShip] = GetIPsForShipIndex(i);
                RegisterDamageHapticListener(fallbackShip);

                continue;
            }

            GameObject newPlayer = Instantiate(PlayerPrefab, spawnPosition, Quaternion.identity);
            newPlayer.GetComponent<BoatHealth>().audioSource = audioSource;

            if (data.lobbyType == LobbyType.OnePlayer)
                newPlayer.GetComponent<BoatUI2>().ChangeName(data.playerList[i].NickName);
            else
                newPlayer.GetComponent<BoatUI2>().ChangeName(data.playerList[i * 2].NickName);

            PlayerObjects.Add(newPlayer);
            ApplyAbilityPassives(data.playerList[f], newPlayer);
            RegisterWinConditionListener(newPlayer);

            // NEW
            shipToIPs[newPlayer] = GetIPsForShipIndex(i);
            RegisterDamageHapticListener(newPlayer);
        }
    }

    private void RegisterWinConditionListener(GameObject ship)
    {
        if (ship == null) return;

        BoatHealth health = ship.GetComponent<BoatHealth>();
        if (health != null)
        {
            health.onDeath.AddListener(CheckWinCondition);
        }
    }

    // Counts ships still alive (currentHealth > 0) among PlayerObjects. If
    // only one (or zero, on a simultaneous double-death) remains, ends the
    // game. Skipped entirely for single-ship games (e.g. solo testing),
    // since a 1-ship match would otherwise "win" the instant it spawns.
    private void CheckWinCondition()
    {
        if (gameEnded) return;
        if (PlayerObjects == null || PlayerObjects.Count <= 1) return;

        int aliveCount = 0;
        GameObject lastAlive = null;

        foreach (var ship in PlayerObjects)
        {
            if (ship == null) continue;

            BoatHealth health = ship.GetComponent<BoatHealth>();
            if (health != null && !health.IsDead)
            {
                aliveCount++;
                lastAlive = ship;
            }
        }

        if (aliveCount <= 1)
        {
            EndGame(lastAlive);
        }
    }

    // NEW: resolves the display nickname for a ship, using the same
    // index convention BoatUI2.ChangeName() already uses at spawn time -
    // i*2 (the mover) for Two-Player ships, i for One-Player ships - so
    // the winner's name matches whatever nameplate was already shown
    // above that ship during the match.
    private string GetShipNickname(GameObject ship)
    {
        if (ship == null || PlayerObjects == null) return "Unknown";

        int i = PlayerObjects.IndexOf(ship);
        if (i < 0) return ship.name; // shouldn't happen, defensive fallback

        int nicknameIndex = (data.lobbyType == LobbyType.TwoPlayer) ? i * 2 : i;

        if (nicknameIndex < 0 || nicknameIndex >= data.playerList.Count)
            return ship.name; // defensive fallback

        return data.playerList[nicknameIndex].NickName;
    }

    private void EndGame(GameObject lastAlive)
    {
        gameEnded = true;

        // CHANGED: resolve the player's nickname instead of using the raw
        // ship GameObject name (e.g. "Ship(Clone)").
        string winnerNickname = lastAlive != null ? GetShipNickname(lastAlive) : null;

        Debug.Log(lastAlive != null
            ? $"[GameHandler] Game over - {winnerNickname} wins!"
            : "[GameHandler] Game over - no ships remain.");

        FreezeAllShips();

        if (winUIObject != null)
            winUIObject.SetActive(true);

        if (WinnerNameText != null)
            WinnerNameText.text = lastAlive != null ? winnerNickname : "No one";
    }

    private void FreezeAllShips()
    {
        foreach (var ship in PlayerObjects)
        {
            if (ship == null) continue;

            Rigidbody rb = ship.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            BoatCont boatCont = ship.GetComponent<BoatCont>();
            if (boatCont != null) boatCont.enabled = false;

            CanonWork canonWork = ship.GetComponent<CanonWork>();
            if (canonWork != null) canonWork.enabled = false;
        }
    }

    void ApplyAbilityPassives(PlayerData data, GameObject player)
    {
        if (data.ability == Ability.Healer)
        {
            player.GetComponent<CanonWork>().CanonDamage = 5f;
        }
        else if (data.ability == Ability.Invicible)
        {
            player.GetComponent<BoatHealth>().maxHealth = 50f;
            player.GetComponent<BoatHealth>().currentHealth = 50f;
        }
        else if (data.ability == Ability.Randy)
        {
            player.GetComponent<CanonWork>().CanonDamage = UnityEngine.Random.Range(1f, 10f);
        }
        else if (data.ability == Ability.Striker)
        {
            BoatCont boatCont = player.GetComponent<BoatCont>();
            if (boatCont != null)
            {
                boatCont.accelerationForce *= 1.4f;
                boatCont.maxSpeed *= 1.3f;
            }
        }
        else if (data.ability == Ability.Sniper)
        {
            player.GetComponent<CanonWork>().CanonDamage = 12f;
            player.GetComponent<BoatHealth>().maxHealth = 70f;
            player.GetComponent<BoatHealth>().currentHealth = 70f;
        }
        else if (data.ability == Ability.Tank)
        {
            player.GetComponent<BoatHealth>().maxHealth = 150f;
            player.GetComponent<BoatHealth>().currentHealth = 150f;

            BoatCont boatCont = player.GetComponent<BoatCont>();
            if (boatCont != null)
                boatCont.accelerationForce *= 0.75f;
        }
        else if (data.ability == Ability.Saboteur)
        {
            // No baseline stat change - Saboteur relies entirely on its
            // active sabotage rather than a passive buff/debuff.
        }
        else if (data.ability == Ability.Phantom)
        {
            BoatCont boatCont = player.GetComponent<BoatCont>();
            if (boatCont != null)
                boatCont.turnSpeed *= 1.3f;
        }
        else if (data.ability == Ability.Berserker)
        {
            player.GetComponent<BoatHealth>().maxHealth = 130f;
            player.GetComponent<BoatHealth>().currentHealth = 130f;
            player.GetComponent<CanonWork>().CanonDamage = 3f;
        }

        BoatHealth boatHealth = player.GetComponent<BoatHealth>();
        if (boatHealth != null)
            boatHealth.SetHullColor(Abilities.Get(data.ability).HullColor);
    }
}