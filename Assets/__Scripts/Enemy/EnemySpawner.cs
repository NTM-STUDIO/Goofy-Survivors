    using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    private enum SpawnSide { Left, Right, Top, Bottom }

    [Header("Wave Configuration")]
    public List<Wave> waves;
    public int waveIndex = 0;

    [Header("Spawning Position")]
    [Tooltip("A coordenada Y do seu chão onde os inimigos devem aparecer.")]
    public float groundLevelY = 0f;
    [Tooltip("Quão longe fora da visão da câmera os inimigos devem aparecer?")]
    public float spawnBuffer = 1f;
    
    [Tooltip("Uma grande distância para usar como fallback se um raio da câmera não atingir o chão.")]
    public float raycastFallbackDistance = 200f;

    private Camera mainCamera;

    void Start()
    {

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("EnemySpawner Error: Main Camera not found! Make sure your camera is tagged 'MainCamera'.");
        }
    }

    private int GetPlayerCountMultiplier()
    {
        // Check if we're in a networked game
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // In multiplayer, count connected clients
            if (NetworkManager.Singleton.IsServer)
            {
                int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
                return Mathf.Max(1, playerCount); // Ensure at least 1
            }
        }
        
        // Single-player or not server: return 1
        return 1;
    }

    public void StartSpawning()
    {
        Debug.Log("EnemySpawner: Starting wave spawning.");
        if (waves != null && waves.Count > 0)
            StartCoroutine(SpawnWaves());
        else
            Debug.LogWarning("EnemySpawner: No waves assigned in inspector.");
    }

    public void ResetForRestart()
    {
        // Stop any ongoing spawn coroutine(s) and reset wave index
        try { StopAllCoroutines(); } catch {}
        waveIndex = 0;
        Debug.Log("EnemySpawner: ResetForRestart called. Coroutines stopped and waveIndex reset to 0.");
    }

    IEnumerator SpawnWaves()
    {
        Debug.Log("EnemySpawner: Beginning wave spawning.");
        yield return null; // Espera um frame para garantir que tudo foi inicializado
        while (waveIndex < waves.Count)
        {
            Wave currentWave = waves[waveIndex];
            Debug.Log("Starting Wave: " + (currentWave.waveName != "" ? currentWave.waveName : (waveIndex + 1).ToString()));
            
            // Calculate multiplier based on number of connected players
            int playerMultiplier = GetPlayerCountMultiplier();
            Debug.Log($"EnemySpawner: Player multiplier is {playerMultiplier}");
            
            List<int> remainingCounts = new List<int>();
            int totalEnemiesToSpawn = 0;
            foreach (WaveEnemy waveEnemy in currentWave.enemies)
            {
                // Multiply enemy count by number of players
                int clampedCount = Mathf.Max(0, waveEnemy.enemyCount * playerMultiplier);
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
                        
                        // If we're running a networked game and we're the server, spawn as a NetworkObject.
                        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                        {
                            // Ensure prefab is registered and has a NetworkObject so it replicates to clients
                            RuntimeNetworkPrefabRegistry.TryRegister(selectedEnemy.enemyPrefab);
                            GameObject spawned = Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);
                            var netObj = spawned.GetComponent<NetworkObject>();
                            if (netObj == null)
                            {
                                netObj = spawned.AddComponent<NetworkObject>();
                            }
                            // Server spawns the network object so it is replicated to clients.
                            netObj.Spawn(true);
                        }
                        else
                        {
                            // Single-player or no network: classic instantiate.
                            Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);
                        }

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

    // --- FUNÇÃO DE POSIÇÃO DE SPAWN TOTALMENTE REESCRITA E CORRIGIDA ---
    Vector3 GetSpawnPosition3D(SpawnSide side)
    {
        //Debug.Log("Calculating spawn position for side: " + side.ToString());
        if (mainCamera == null) return Vector3.zero;

        // Cria um plano infinito na altura do chão.
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevelY, 0));

        // Obtém os quatro cantos da tela projetados no plano do chão.
        // Estes pontos definem o quadrilátero de visão no mundo.
        Vector3 worldBottomLeft = GetWorldPointOnPlane(new Vector2(0, 0), groundPlane);
        Vector3 worldBottomRight = GetWorldPointOnPlane(new Vector2(1, 0), groundPlane);
        Vector3 worldTopLeft = GetWorldPointOnPlane(new Vector2(0, 1), groundPlane);
        Vector3 worldTopRight = GetWorldPointOnPlane(new Vector2(1, 1), groundPlane);
        
        Vector3 spawnPoint = Vector3.zero;
        Vector3 offsetDirection = Vector3.zero;

        // Em vez de uma caixa, usamos as bordas reais do quadrilátero de visão.
        switch (side)
        {
            case SpawnSide.Left:
                // Escolhe um ponto aleatório ao longo da borda esquerda.
                spawnPoint = Vector3.Lerp(worldBottomLeft, worldTopLeft, Random.value);
                // Calcula uma direção para fora, perpendicular a essa borda.
                offsetDirection = (worldTopLeft - worldBottomLeft).normalized;
                offsetDirection = new Vector3(-offsetDirection.z, 0, offsetDirection.x); // Rotaciona 90 graus
                break;

            case SpawnSide.Right:
                // Escolhe um ponto aleatório ao longo da borda direita.
                spawnPoint = Vector3.Lerp(worldBottomRight, worldTopRight, Random.value);
                offsetDirection = (worldTopRight - worldBottomRight).normalized;
                offsetDirection = new Vector3(offsetDirection.z, 0, -offsetDirection.x);
                break;

            case SpawnSide.Top:
                // Escolhe um ponto aleatório ao longo da borda superior.
                spawnPoint = Vector3.Lerp(worldTopLeft, worldTopRight, Random.value);
                offsetDirection = (worldTopRight - worldTopLeft).normalized;
                offsetDirection = new Vector3(offsetDirection.z, 0, -offsetDirection.x);
                break;

            case SpawnSide.Bottom:
                // Escolhe um ponto aleatório ao longo da borda inferior.
                spawnPoint = Vector3.Lerp(worldBottomLeft, worldBottomRight, Random.value);
                offsetDirection = (worldBottomRight - worldBottomLeft).normalized;
                offsetDirection = new Vector3(-offsetDirection.z, 0, offsetDirection.x);
                break;
        }

        // Aplica o buffer na direção correta para fora da tela.
        return spawnPoint + offsetDirection.normalized * spawnBuffer;
    }

    // Função auxiliar para encontrar pontos de interseção de forma robusta
    Vector3 GetWorldPointOnPlane(Vector2 viewportCoord, Plane plane)
    {
        //Debug.Log("EnemySpawner: Getting world point on plane for viewport coord: " + viewportCoord);
        Ray ray = mainCamera.ViewportPointToRay(viewportCoord);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        else
        {
            Debug.LogWarning($"Camera ray at viewport {viewportCoord} did not intersect ground plane. Using fallback distance.");
            return ray.GetPoint(raycastFallbackDistance);
        }
    }

    // Esta função permanece inalterada
    SpawnSide ChooseBalancedSpawnSide()
    {
        //Debug.Log("EnemySpawner: Choosing balanced spawn side.");
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

    public void RespawnEnemy(GameObject enemyToRespawn)
    {
        if (enemyToRespawn == null) return;
        SpawnSide spawnSide = ChooseBalancedSpawnSide();
        Vector3 spawnPos = GetSpawnPosition3D(spawnSide);
        // If networked and we're server, just reposition and reactivate; state should already be synchronized via NetworkVariables.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            enemyToRespawn.transform.position = spawnPos;
            enemyToRespawn.SetActive(true);
        }
        else
        {
            enemyToRespawn.transform.position = spawnPos;
            enemyToRespawn.SetActive(true);
        }
    }
}

// Estas classes permanecem as mesmas
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