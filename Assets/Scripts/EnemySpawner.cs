using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public List<Wave> waves;
    public int waveIndex = 0; // Current wave
    private Camera mainCamera;
    void Start()
    {
        mainCamera = Camera.main;

        // Ensure there are waves to spawn
        if (waves != null && waves.Count > 0)
            StartCoroutine(SpawnWaves());
        else
            Debug.LogWarning("EnemySpawner: No waves assigned in inspector.");
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

            float healthMultiplier = Mathf.Pow(1 + currentWave.healthMultiplierPerWave, waveIndex);

            foreach (WaveEnemy waveEnemy in currentWave.enemies)
            {
                for (int i = 0; i < waveEnemy.enemyCount; i++)
                {
                    Vector2 spawnPos = GetSpawnPositionOutsideCamera();
                    GameObject enemy = Instantiate(waveEnemy.enemyPrefab, spawnPos, Quaternion.identity);

                    EnemyController controller = enemy.GetComponent<EnemyController>();
                    if (controller != null)
                        controller.SetStats(healthMultiplier);

                    yield return new WaitForSeconds(currentWave.spawnInterval);
                }
            }
            waveIndex++;
        }
        Debug.Log("All waves completed!");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
