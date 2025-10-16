using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    private enum SpawnSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    [Header("Wave Configuration")]
    public List<Wave> waves;
    public int waveIndex = 0; // Current wave

    [Header("Spawning Position")]
    [Tooltip("The Y-coordinate of your ground level where enemies should spawn.")]
    public float groundLevelY = 0f;
    [Tooltip("How far outside the camera view should enemies spawn?")]
    public float spawnBuffer = 3f;

    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("EnemySpawner Error: Main Camera not found! Make sure your camera is tagged 'MainCamera'.");
        }
    }

    public void StartSpawning()
    {
        if (waves != null && waves.Count > 0)
            StartCoroutine(SpawnWaves());
        else
            Debug.LogWarning("EnemySpawner: No waves assigned in inspector.");
    }

    IEnumerator SpawnWaves()
    {
        // Wait a frame to ensure the camera is fully initialized
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
                        
                        // --- 3D CHANGE: Use the new 3D position calculation ---
                        Vector3 spawnPos = GetSpawnPosition3D(spawnSide);
                        
                        // Spawn at the calculated position with no rotation
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

    // --- NEW 3D-AWARE SPAWN POSITION FUNCTION ---
    Vector3 GetSpawnPosition3D(SpawnSide side)
    {
        if (mainCamera == null) return Vector3.zero;

        // Create a mathematical plane at our ground level, facing upwards
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevelY, 0));

        // Define the four corners of the screen in viewport space
        Vector3 viewportBottomLeft = new Vector3(0, 0, 0);
        Vector3 viewportTopRight = new Vector3(1, 1, 0);

        // Create rays from the camera through the screen corners
        Ray rayBottomLeft = mainCamera.ViewportPointToRay(viewportBottomLeft);
        Ray rayTopRight = mainCamera.ViewportPointToRay(viewportTopRight);

        // Find where these rays hit the ground plane
        Vector3 worldBottomLeft, worldTopRight;
        if (groundPlane.Raycast(rayBottomLeft, out float blDistance))
        {
            worldBottomLeft = rayBottomLeft.GetPoint(blDistance);
        }
        else
        {
            Debug.LogError("Camera ray for bottom-left corner does not intersect the ground plane!");
            return Vector3.zero; // Or a fallback position
        }

        if (groundPlane.Raycast(rayTopRight, out float trDistance))
        {
            worldTopRight = rayTopRight.GetPoint(trDistance);
        }
        else
        {
            Debug.LogError("Camera ray for top-right corner does not intersect the ground plane!");
            return Vector3.zero; // Or a fallback position
        }
        
        // Now we have the min/max X and Z coordinates of the view on the ground
        float minX = worldBottomLeft.x - spawnBuffer;
        float maxX = worldTopRight.x + spawnBuffer;
        float minZ = worldBottomLeft.z - spawnBuffer;
        float maxZ = worldTopRight.z + spawnBuffer;

        float spawnX = 0f;
        float spawnZ = 0f;
        
        // Choose a random point just outside these boundaries
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
            case SpawnSide.Top: // "Top" of the screen in an isometric view is likely the positive Z direction
                spawnX = Random.Range(minX, maxX);
                spawnZ = maxZ;
                break;
            case SpawnSide.Bottom: // "Bottom" of the screen is likely the negative Z direction
                spawnX = Random.Range(minX, maxX);
                spawnZ = minZ;
                break;
        }

        return new Vector3(spawnX, groundLevelY, spawnZ);
    }

    // This balancing function works fine as it is, since it operates in viewport space.
    SpawnSide ChooseBalancedSpawnSide()
    {
        // ... (No changes needed in this function)
        SpawnSide fallback = (SpawnSide)Random.Range(0, 4);

        if (mainCamera == null)
        {
            return fallback;
        }

        EnemyStats[] activeEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        if (activeEnemies == null || activeEnemies.Length == 0)
        {
            return fallback;
        }

        const float viewportMargin = 0.15f;
        int leftCount = 0;
        int rightCount = 0;
        int topCount = 0;
        int bottomCount = 0;

        foreach (EnemyStats enemy in activeEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 viewport = mainCamera.WorldToViewportPoint(enemy.transform.position);

            if (viewport.z < 0f)
            {
                continue;
            }

            if (viewport.x < -viewportMargin || viewport.x > 1f + viewportMargin ||
                viewport.y < -viewportMargin || viewport.y > 1f + viewportMargin)
            {
                continue;
            }

            if (viewport.x >= 0.5f)
            {
                rightCount++;
            }
            else
            {
                leftCount++;
            }

            if (viewport.y >= 0.5f)
            {
                topCount++;
            }
            else
            {
                bottomCount++;
            }
        }

        int totalVisible = leftCount + rightCount;
        if (totalVisible == 0)
        {
            return fallback;
        }

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
            if (counts[i] > maxCount)
            {
                maxCount = counts[i];
            }
        }

        if (maxCount == 0)
        {
            return fallback;
        }

        float totalWeight = 0f;
        for (int i = 0; i < counts.Length; i++)
        {
            float bias = (maxCount - counts[i]) + 1f;
            float weight = bias * bias;
            weights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            return fallback;
        }

        float roll = Random.value * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return (SpawnSide)i;
            }
        }

        return fallback;
    }
}


// These two classes can remain exactly the same.
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