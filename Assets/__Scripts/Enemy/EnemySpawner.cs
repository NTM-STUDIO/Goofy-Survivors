using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;


public class EnemySpawner : MonoBehaviour
{
    private enum SpawnSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    public List<Wave> waves;
    public int waveIndex = 0; // Current wave
    private Camera mainCamera;

    // Use Awake() for initialization that needs to happen before other scripts run.
    void Awake()
    {
        mainCamera = Camera.main;

        // Add a check to be safe and provide a clearer error message.
        if (mainCamera == null)
        {
            Debug.LogError("EnemySpawner Error: Main Camera not found! Make sure your camera is tagged 'MainCamera'.");
        }
    }

    SpawnSide ChooseBalancedSpawnSide()
    {
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

    Vector2 GetSpawnPositionOutsideCamera(SpawnSide side)
    {
        if (mainCamera == null)
        {
            return Vector2.zero;
        }

        // Extend the camera bounds by a buffer to ensure spawning is off-screen
        float buffer = 1.5f;
        Vector2 min = mainCamera.ViewportToWorldPoint(new Vector2(0, 0));
        Vector2 max = mainCamera.ViewportToWorldPoint(new Vector2(1, 1));

        float spawnX = 0f;
        float spawnY = 0f;

        switch (side)
        {
            case SpawnSide.Left:
                spawnX = min.x - buffer;
                spawnY = Random.Range(min.y, max.y);
                break;
            case SpawnSide.Right:
                spawnX = max.x + buffer;
                spawnY = Random.Range(min.y, max.y);
                break;
            case SpawnSide.Top:
                spawnX = Random.Range(min.x, max.x);
                spawnY = max.y + buffer;
                break;
            case SpawnSide.Bottom:
                spawnX = Random.Range(min.x, max.x);
                spawnY = min.y - buffer;
                break;
        }

        return new Vector2(spawnX, spawnY);
    }

    IEnumerator SpawnWaves()
    {
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
<<<<<<< Updated upstream
                        Vector2 spawnPos = GetSpawnPositionOutsideCamera(spawnSide);
                        GameObject enemy = Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);
=======
                        Vector3 spawnPos = GetSpawnPosition3D(spawnSide);
                        
                        GameObject spawned = Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);
                        // If we're in P2P (hosted) mode, ensure the host spawns the network object so clients receive it.
                        if (GameManager.Instance != null && GameManager.Instance.isP2P)
                        {
                            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                            {
                                var netObj = spawned.GetComponent<NetworkObject>();
                                if (netObj != null)
                                {
                                    netObj.Spawn();
                                }
                                else
                                {
                                    Debug.LogWarning("Spawned enemy prefab does not have a NetworkObject. Add one to sync over network.", spawned);
                                }
                            }
                            else
                            {
                                // In a P2P client, don't locally spawn enemies - the host will spawn and sync them.
                                Destroy(spawned);
                            }
                        }
>>>>>>> Stashed changes

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


    public void StartSpawning()
    {
        if (waves != null && waves.Count > 0)
            StartCoroutine(SpawnWaves());
        else
            Debug.LogWarning("EnemySpawner: No waves assigned in inspector.");
    }
}


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