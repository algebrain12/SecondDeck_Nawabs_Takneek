using UnityEngine;
using System.Collections.Generic;

public class BombSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The SpawnAreaDisplayer whose generated points bombs will spawn at.")]
    public SpawnAreaDisplayer spawnArea;

    [Tooltip("The bomb prefab to instantiate at each spawn point.")]
    public GameObject bombPrefab;

    [Header("Spawn Settings")]
    [Tooltip("How many bombs to spawn. Overrides spawnArea.pointCount when spawning.")]
    [Min(0)] public int bombCount = 5;

    [Tooltip("Y offset applied on top of each generated point (e.g. to spawn bombs slightly above the water).")]
    public float spawnHeightOffset = 73f;

    [Tooltip("If true, spawns bombs once automatically in Start().")]
    public bool spawnOnStart = true;

    [Header("Repeating Spawns (optional)")]
    [Tooltip("If true, re-spawns a fresh batch of bombs on a timer.")]
    public bool useRepeatingSpawns = false;

    [Tooltip("Seconds between repeat spawns (only used if useRepeatingSpawns is true).")]
    [Min(0.1f)] public float spawnInterval = 10f;

    [Tooltip("If true, previously spawned bombs are destroyed before each new batch.")]
    public bool clearPreviousBombsOnRespawn = true;

    private readonly List<GameObject> spawnedBombs = new List<GameObject>();
    private float repeatTimer = 0f;

    public AudioSource audioSource;

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnBombs();
        }

        repeatTimer = spawnInterval;
    }

    void Update()
    {
        if (!useRepeatingSpawns) return;

        repeatTimer -= Time.deltaTime;
        if (repeatTimer <= 0f)
        {
            SpawnBombs();
            repeatTimer = spawnInterval;
        }
    }

    /// <summary>
    /// Generates fresh points via the SpawnAreaDisplayer (using bombCount as the
    /// desired point count) and instantiates a bomb prefab at each one.
    /// </summary>
    public void SpawnBombs()
    {
        if (spawnArea == null)
        {
            Debug.LogError($"[BombSpawner] No SpawnAreaDisplayer assigned on '{name}'.");
            return;
        }

        if (bombPrefab == null)
        {
            Debug.LogError($"[BombSpawner] No bombPrefab assigned on '{name}'.");
            return;
        }

        if (clearPreviousBombsOnRespawn)
        {
            ClearSpawnedBombs();
        }

        // Sync the requested count and regenerate points on the shared area component.
        spawnArea.pointCount = bombCount;
        spawnArea.GeneratePoints();

        if (spawnArea.generatedPoints.Count < bombCount)
        {
            Debug.LogWarning(
                $"[BombSpawner] Only got {spawnArea.generatedPoints.Count}/{bombCount} spawn points from " +
                $"'{spawnArea.name}'. Fewer bombs than requested will be spawned.");
        }

        foreach (Vector3 point in spawnArea.generatedPoints)
        {
            Vector3 spawnPos = point + Vector3.up * spawnHeightOffset;
            GameObject bomb = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
            if (bomb.GetComponent<BombScript>() != null)
            {
                bomb.GetComponent<BombScript>().audioSource = audioSource;;
            }
            spawnedBombs.Add(bomb);
        }
    }

    /// <summary>
    /// Destroys any bombs currently tracked as spawned by this spawner.
    /// </summary>
    public void ClearSpawnedBombs()
    {
        for (int i = 0; i < spawnedBombs.Count; i++)
        {
            if (spawnedBombs[i] != null)
                Destroy(spawnedBombs[i]);
        }
        spawnedBombs.Clear();
    }
}