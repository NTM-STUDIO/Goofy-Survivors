using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    public List<Wave> waves;
    public int waveIndex = 0;
    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("EnemySpawner Error: Main Camera not found! Make sure your camera is tagged 'MainCamera'.");
        }
    }

    void Start()
    {
        StartSpawning();
    }

    Vector2 GetSpawnPositionOutsideCamera()
    {
        float buffer = 1.5f;
        Vector2 min = mainCamera.ViewportToWorldPoint(new Vector2(0, 0));
        Vector2 max = mainCamera.ViewportToWorldPoint(new Vector2(1, 1));
        float spawnX, spawnY;

        if (Random.value < 0.5f)
        {
            spawnX = Random.value < 0.5f ? min.x - buffer : max.x + buffer;
            spawnY = Random.Range(min.y, max.y);
        }
        else
        {
            spawnX = Random.Range(min.x, max.x);
            spawnY = Random.value < 0.5f ? min.y - buffer : max.y + buffer;
        }
        return new Vector2(spawnX, spawnY);
    }

    IEnumerator SpawnWaves()
    {
        // ADDED DEBUG: Log how many waves are configured.
        Debug.Log($"Spawner starting with {waves.Count} total waves configured.");

        while (waveIndex < waves.Count)
        {
            Wave currentWave = waves[waveIndex];
            Debug.Log($"<color=yellow>--- Starting Wave {waveIndex + 1}: {(currentWave.waveName != "" ? currentWave.waveName : "Unnamed")} ---</color>");

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
                Debug.LogWarning($"Wave '{currentWave.waveName}' has no enemies to spawn. Skipping.");
            }
            else
            {
                // ADDED DEBUG: Announce the total number of enemies for this wave.
                Debug.Log($"Wave {waveIndex + 1} will spawn a total of {totalEnemiesToSpawn} enemies.");
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

                        // ADDED DEBUG: Announce which enemy is being spawned.
                        Debug.Log($"Spawning '{selectedEnemy.enemyPrefab.name}'. {totalEnemiesToSpawn - 1} enemies left in wave.");
                        
                        Vector2 spawnPos = GetSpawnPositionOutsideCamera();
                        Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);

                        remainingCounts[enemyIndex]--;
                        totalEnemiesToSpawn--;

                        yield return new WaitForSeconds(currentWave.spawnInterval);
                        break;
                    }
                }
            }

            // ADDED DEBUG: Announce that the wave's spawning is complete and the wait is beginning.
            Debug.Log($"<color=green>Wave {waveIndex + 1} spawning complete. Waiting for {currentWave.timeUntilNextWave} seconds...</color>");
            
            yield return new WaitForSeconds(currentWave.timeUntilNextWave);

            waveIndex++;
        }
        
        Debug.Log("<color=cyan>--- All waves completed! ---</color>");
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