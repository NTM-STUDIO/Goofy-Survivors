using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    // ... (Your enums and other variables remain the same)
    private enum SpawnSide { Left, Right, Top, Bottom }
    [Header("Wave Configuration")]
    public List<Wave> waves;
    public int waveIndex = 0;
    [Header("Spawning Position")]
    [Tooltip("The Y-coordinate of your ground level where enemies should spawn.")]
    public float groundLevelY = 0f;
    [Tooltip("How far outside the camera view should enemies spawn?")]
    public float spawnBuffer = 3f;
    
    // --- NEW VARIABLE ---
    [Tooltip("A large distance to use as a fallback if a camera ray doesn't hit the ground (e.g., when looking at the horizon in perspective).")]
    public float raycastFallbackDistance = 200f;

    private Camera mainCamera;

    void Awake()
    {
        // ... (Awake remains the same)
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("EnemySpawner Error: Main Camera not found! Make sure your camera is tagged 'MainCamera'.");
        }
    }

    public void StartSpawning()
    {
        // ... (StartSpawning remains the same)
        if (waves != null && waves.Count > 0)
            StartCoroutine(SpawnWaves());
        else
            Debug.LogWarning("EnemySpawner: No waves assigned in inspector.");
    }

    IEnumerator SpawnWaves()
    {
        // ... (SpawnWaves coroutine remains the same)
        yield return null;
        while (waveIndex < waves.Count)
        {
            Wave currentWave = waves[waveIndex];
            Debug.Log("Starting Wave: " + (currentWave.waveName != "" ? currentWave.waveName : (waveIndex + 1).ToString()));
            List<int> remainingCounts = new List<int>();
            int totalEnemiesToSpawn = 0;
            foreach (WaveEnemy waveEnemy in currentWave.enemies)
            {
                int clampedCount = Mathf.Max(0, waveEnemy.enemyCount);
                remainingCounts.Add(clampedCount);
                totalEnemiesToSpawn += clampedCount;
            }
            if (totalEnemiesToSpawn == 0)
            {
                Debug.LogWarning($"Wave '{currentWave.waveName}' has no enemies with a positive count.");
            }
            while (totalEnemiesToSpawn > 0)
            {
                int roll = Random.Range(0, totalEnemiesToSpawn);
                int cumulative = 0;
                for (int enemyIndex = 0; enemyIndex < currentWave.enemies.Count; enemyIndex++)
                {
                    if (remainingCounts[enemyIndex] == 0)
                        continue;
                    cumulative += remainingCounts[enemyIndex];
                    if (roll < cumulative)
                    {
                        WaveEnemy selectedEnemy = currentWave.enemies[enemyIndex];
                        SpawnSide spawnSide = ChooseBalancedSpawnSide();
                        Vector3 spawnPos = GetSpawnPosition3D(spawnSide);
                        Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);
                        remainingCounts[enemyIndex]--;
                        totalEnemiesToSpawn--;
                        yield return new WaitForSeconds(currentWave.spawnInterval);
                        break;
                    }
                }
            }
            yield return new WaitForSeconds(currentWave.timeUntilNextWave);
            waveIndex++;
        }
        Debug.Log("All waves completed!");
    }

    // --- FULLY REWRITTEN AND ROBUST SPAWN POSITION FUNCTION ---
    Vector3 GetSpawnPosition3D(SpawnSide side)
    {
        if (mainCamera == null) return Vector3.zero;

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevelY, 0));

        // Get all four corners of the screen on the ground plane
        Vector3 worldBottomLeft = GetWorldPointOnPlane(new Vector2(0, 0), groundPlane);
        Vector3 worldBottomRight = GetWorldPointOnPlane(new Vector2(1, 0), groundPlane);
        Vector3 worldTopLeft = GetWorldPointOnPlane(new Vector2(0, 1), groundPlane);
        Vector3 worldTopRight = GetWorldPointOnPlane(new Vector2(1, 1), groundPlane);
        
        // Find the absolute min and max coordinates from the four points
        float minX = Mathf.Min(worldBottomLeft.x, worldTopLeft.x) - spawnBuffer;
        float maxX = Mathf.Max(worldBottomRight.x, worldTopRight.x) + spawnBuffer;
        float minZ = Mathf.Min(worldBottomLeft.z, worldBottomRight.z) - spawnBuffer;
        float maxZ = Mathf.Max(worldTopLeft.z, worldTopRight.z) + spawnBuffer;

        float spawnX = 0f;
        float spawnZ = 0f;
        
        switch (side)
        {
            case SpawnSide.Left:
                spawnX = minX;
                spawnZ = Random.Range(minZ, maxZ);
                break;
            case SpawnSide.Right:
                spawnX = maxX;
                spawnZ = Random.Range(minZ, maxZ);
                break;
            case SpawnSide.Top:
                spawnX = Random.Range(minX, maxX);
                spawnZ = maxZ;
                break;
            case SpawnSide.Bottom:
                spawnX = Random.Range(minX, maxX);
                spawnZ = minZ;
                break;
        }

        return new Vector3(spawnX, groundLevelY, spawnZ);
    }
    
    // --- NEW HELPER FUNCTION TO ROBUSTLY FIND INTERSECTION POINTS ---
    Vector3 GetWorldPointOnPlane(Vector2 viewportCoord, Plane plane)
    {
        Ray ray = mainCamera.ViewportPointToRay(viewportCoord);
        if (plane.Raycast(ray, out float distance))
        {
            // Success: The ray hit the plane, return the intersection point
            return ray.GetPoint(distance);
        }
        else
        {
            // Failure: The ray is parallel or points away from the plane
            // Return a fallback point far along the ray's direction.
            Debug.LogWarning($"Camera ray at viewport {viewportCoord} did not intersect ground plane. Using fallback distance.");
            return ray.GetPoint(raycastFallbackDistance);
        }
    }


    SpawnSide ChooseBalancedSpawnSide()
    {
        // ... (This function remains unchanged and is perfectly fine)
        SpawnSide fallback = (SpawnSide)Random.Range(0, 4);
        if (mainCamera == null) return fallback;
        EnemyStats[] activeEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        if (activeEnemies == null || activeEnemies.Length == 0) return fallback;
        const float viewportMargin = 0.15f;
        int leftCount = 0;
        int rightCount = 0;
        int topCount = 0;
        int bottomCount = 0;
        foreach (EnemyStats enemy in activeEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            Vector3 viewport = mainCamera.WorldToViewportPoint(enemy.transform.position);
            if (viewport.z < 0f) continue;
            if (viewport.x < -viewportMargin || viewport.x > 1f + viewportMargin || viewport.y < -viewportMargin || viewport.y > 1f + viewportMargin) continue;
            if (viewport.x >= 0.5f) rightCount++;
            else leftCount++;
            if (viewport.y >= 0.5f) topCount++;
            else bottomCount++;
        }
        int totalVisible = leftCount + rightCount;
        if (totalVisible == 0) return fallback;
        int verticalDiff = topCount - bottomCount;
        int horizontalDiff = rightCount - leftCount;
        const int imbalanceThreshold = 3;
        if (Mathf.Abs(verticalDiff) >= Mathf.Abs(horizontalDiff) && Mathf.Abs(verticalDiff) >= imbalanceThreshold)
        {
            return verticalDiff > 0 ? SpawnSide.Bottom : SpawnSide.Top;
        }
        if (Mathf.Abs(horizontalDiff) >= imbalanceThreshold)
        {
            return horizontalDiff > 0 ? SpawnSide.Left : SpawnSide.Right;
        }
        int[] counts = new int[4];
        counts[(int)SpawnSide.Left] = leftCount;
        counts[(int)SpawnSide.Right] = rightCount;
        counts[(int)SpawnSide.Top] = topCount;
        counts[(int)SpawnSide.Bottom] = bottomCount;
        float[] weights = new float[counts.Length];
        int maxCount = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > maxCount) maxCount = counts[i];
        }
        if (maxCount == 0) return fallback;
        float totalWeight = 0f;
        for (int i = 0; i < counts.Length; i++)
        {
            float bias = (maxCount - counts[i]) + 1f;
            float weight = bias * bias;
            weights[i] = weight;
            totalWeight += weight;
        }
        if (totalWeight <= 0f) return fallback;
        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative) return (SpawnSide)i;
        }
        return fallback;
    }

    // --- NEW RESPAWN FUNCTION ---
    public void RespawnEnemy(GameObject enemyToRespawn)
    {
        if (enemyToRespawn == null) return;

        SpawnSide spawnSide = ChooseBalancedSpawnSide();
        Vector3 spawnPos = GetSpawnPosition3D(spawnSide);

        enemyToRespawn.transform.position = spawnPos;
        enemyToRespawn.SetActive(true);
    }
}


// These two classes remain the same
[System.Serializable]
public class WaveEnemy
{
    public GameObject enemyPrefab;
    public int enemyCount;
}

[System.Serializable]
public class Wave
{
    public string waveName;
    public List<WaveEnemy> enemies;
    public float timeUntilNextWave = 10f;
    public float spawnInterval = 0.5f;
}