using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class EnemySpawner : MonoBehaviour
{
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

    Vector2 GetSpawnPositionOutsideCamera()
    {
        // Extend the camera bounds by a buffer to ensure spawning is off-screen
        float buffer = 1.5f;
        Vector2 min = mainCamera.ViewportToWorldPoint(new Vector2(0, 0));
        Vector2 max = mainCamera.ViewportToWorldPoint(new Vector2(1, 1));

        float spawnX, spawnY;

        // Randomly choose to spawn horizontally or vertically
        if (Random.value < 0.5f)
        {
            // Spawn on left or right edges
            spawnX = Random.value < 0.5f ? min.x - buffer : max.x + buffer;
            spawnY = Random.Range(min.y, max.y);
        }
        else
        {
            // Spawn on top or bottom edges
            spawnX = Random.Range(min.x, max.x);
            spawnY = Random.value < 0.5f ? min.y - buffer : max.y + buffer;
        }

        return new Vector2(spawnX, spawnY);
    }

    IEnumerator SpawnWaves()
    {
        while (waveIndex < waves.Count)
        {
            Wave currentWave = waves[waveIndex];
            Debug.Log("Starting Wave: " + (currentWave.waveName != "" ? currentWave.waveName : (waveIndex + 1).ToString()));

            foreach (WaveEnemy waveEnemy in currentWave.enemies)
            {
                for (int i = 0; i < waveEnemy.enemyCount; i++)
                {
                    Vector2 spawnPos = GetSpawnPositionOutsideCamera();
                    GameObject enemy = Instantiate(waveEnemy.enemyPrefab, spawnPos, Quaternion.identity);


                    yield return new WaitForSeconds(currentWave.spawnInterval);
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