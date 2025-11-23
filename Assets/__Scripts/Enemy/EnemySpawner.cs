using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : NetworkBehaviour
{
    private enum SpawnSide { Left, Right, Top, Bottom }

    // Add this near your other class variables at the top
    private Coroutine spawningCoroutine;

    [Header("Wave Configuration")]
    public List<Wave> waves;
    public int waveIndex = 0;

    [Header("Spawning Position")]
    [Tooltip("A coordenada Y do seu chão onde os inimigos devem aparecer.")]
    public float groundLevelY = 0f;

    [Tooltip("Distância extra ALÉM dos players para spawn invisível")]
    public float spawnBuffer = 30f;

    [Tooltip("Uma grande distância para usar como fallback.")]
    public float raycastFallbackDistance = 200f;

    [Header("Spawn Safety")]
    [Tooltip("Distância mínima de players para spawn seguro")]
    public float minDistanceFromPlayers = 40f;

    [Tooltip("Número máximo de tentativas para encontrar posição segura")]
    public int maxSpawnAttempts = 10;

    [Tooltip("Raio de verificação de colisão no ponto de spawn")]
    public float spawnCollisionCheckRadius = 1f;

    [Header("Visual Effects")]
    [Tooltip("Duração do fade-in ao spawnar")]
    public float fadeInDuration = 0.5f;

    [Header("Debug")]
    [Tooltip("Mostra logs de spawn replacement")]
    public bool showReplacementLogs = false;

    private Camera mainCamera;
    private GameManager gameManager;

    // Cache de posições dos players
    private List<Vector3> playerPositions = new List<Vector3>();

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("EnemySpawner: Main Camera not found initially. Will retry later.");
        }

        gameManager = GameManager.Instance;
    }

    /// <summary>
    /// Stops all spawning coroutines and cleans up any active enemies.
    /// This is called by the GameManager when the game ends or resets to the lobby.
    /// </summary>
    public void StopAndReset()
    {
        Debug.Log("[EnemySpawner] StopAndReset called. Stopping all spawning routines and cleaning up enemies.");

        // 1. Stop the main spawning coroutine
        if (spawningCoroutine != null)
        {
            StopCoroutine(spawningCoroutine);
            spawningCoroutine = null;
        }

        // 2. Stop any other coroutines just in case
        StopAllCoroutines();

        // 3. Reset the wave index for the next game
        waveIndex = 0;

        // 4. Find and destroy all existing enemies
        bool shouldDestroyEnemies = (NetworkManager.Singleton == null) || 
                                   (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        if (shouldDestroyEnemies)
        {
            EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
            Debug.Log($"[EnemySpawner] Destroying {allEnemies.Length} remaining enemies.");

            foreach (var enemy in allEnemies)
            {
                if (enemy == null) continue;

                var netObj = enemy.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(enemy.gameObject);
                }
            }
        }
    }

    // NOVO: Atualiza posições de todos os players na rede
    private void UpdatePlayerPositions()
    {
        playerPositions.Clear();

        // Método 1: Tenta encontrar via NetworkManager (multiplayer)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject != null && client.PlayerObject.gameObject.activeInHierarchy)
                {
                    playerPositions.Add(client.PlayerObject.transform.position);
                }
            }

            if (playerPositions.Count > 0)
            {
                Debug.Log($"EnemySpawner: Found {playerPositions.Count} networked players via NetworkManager.");
                return;
            }
        }

        // Método 2: Tenta encontrar PlayerStats
        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        if (players.Length > 0)
        {
            foreach (PlayerStats player in players)
            {
                if (player != null && player.isActiveAndEnabled)
                {
                    playerPositions.Add(player.transform.position);
                }
            }
            return;
        }

        // Método 3: Fallback por tag
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");

        if (playerObjects.Length > 0)
        {
            foreach (GameObject playerObj in playerObjects)
            {
                if (playerObj != null && playerObj.activeInHierarchy)
                {
                    playerPositions.Add(playerObj.transform.position);
                }
            }
            Debug.LogWarning($"EnemySpawner: Found {playerPositions.Count} players by tag 'Player'.");
            return;
        }

        Debug.LogError("EnemySpawner: No active players found by any method! Spawning will use fallback position.");
    }

    // NOVO: Calcula o centro de todos os players
    private Vector3 GetPlayersCenter()
    {
        if (playerPositions.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in playerPositions)
        {
            sum += pos;
        }
        return sum / playerPositions.Count;
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
                return Mathf.Max(1, playerCount);
            }
        }

        // Single-player or not server: return 1
        return 1;
    }

    public void StartSpawning()
    {
        Debug.Log("EnemySpawner: Starting wave spawning.");
        if (waves != null && waves.Count > 0)
        {
            // Ensure we stop any old coroutine before starting a new one
            if (spawningCoroutine != null)
            {
                StopCoroutine(spawningCoroutine);
            }
            spawningCoroutine = StartCoroutine(SpawnWaves());
        }
        else
        {
            Debug.LogWarning("EnemySpawner: No waves assigned in inspector.");
        }
    }

    public void ResetForRestart()
    {
        StopAndReset();
        Debug.Log("EnemySpawner: ResetForRestart called.");
    }

    private IEnumerator SpawnWaves()
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
                waveIndex++;
                yield return new WaitForSeconds(currentWave.timeUntilNextWave);
                continue;
            }

            while (totalEnemiesToSpawn > 0)
            {
                // NOVO: Atualiza posições dos players a cada spawn
                UpdatePlayerPositions();

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

                        // NOVO: Tenta encontrar posição segura baseada em TODOS os players
                        Vector3 spawnPos = Vector3.zero;
                        bool foundSafePosition = false;

                        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
                        {
                            SpawnSide spawnSide = ChooseBalancedSpawnSide();
                            Vector3 candidatePos = GetSpawnPositionForAllPlayers(spawnSide);

                            if (IsSpawnPositionSafe(candidatePos))
                            {
                                spawnPos = candidatePos;
                                foundSafePosition = true;
                                break;
                            }
                        }

                        if (!foundSafePosition)
                        {
                            Debug.LogWarning("EnemySpawner: Could not find safe spawn position after " + maxSpawnAttempts + " attempts. Using fallback.");
                            SpawnSide spawnSide = ChooseBalancedSpawnSide();
                            spawnPos = GetSpawnPositionForAllPlayers(spawnSide);
                        }

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

                            // NOVO: Adiciona fade-in que funciona em todos os clientes
                            var fadeEffect = spawned.AddComponent<EnemyFadeEffect>();
                            fadeEffect.StartFadeIn(fadeInDuration);

                            // If the midgame global mutation is active, apply it server-side to the newly spawned enemy
                            if (GameManager.Instance != null && GameManager.Instance.GetGlobalMidgameMutation() != MutationType.None)
                            {
                                var es = spawned.GetComponent<EnemyStats>();
                                if (es != null)
                                {
                                    GameManager.Instance.ApplyMidgameMutationToEnemy(es);
                                }
                            }
                        }
                        else
                        {
                            // Single-player or no network: classic instantiate.
                            GameObject spawned = Instantiate(selectedEnemy.enemyPrefab, spawnPos, Quaternion.identity);
                            var fadeEffect = spawned.AddComponent<EnemyFadeEffect>();
                            fadeEffect.StartFadeIn(fadeInDuration);

                            if (GameManager.Instance != null && GameManager.Instance.GetGlobalMidgameMutation() != MutationType.None)
                            {
                                var es = spawned.GetComponent<EnemyStats>();
                                if (es != null)
                                {
                                    GameManager.Instance.ApplyMidgameMutationToEnemy(es);
                                }
                            }
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
    private Vector3 GetSpawnPosition3D(SpawnSide side)
    {
        if (mainCamera == null) return Vector3.zero;

        // Cria um plano infinito na altura do chão.
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, groundLevelY, 0));

        // Obtém os quatro cantos da tela projetados no plano do chão.
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

    // NOVO: Calcula spawn baseado em TODOS os players, não apenas câmera local
    private Vector3 GetSpawnPositionForAllPlayers(SpawnSide side)
    {
        if (playerPositions.Count == 0)
        {
            Debug.LogError("EnemySpawner: No players found! Cannot calculate spawn position. Attempting to update player list...");
            UpdatePlayerPositions();

            // Se ainda não há players, usa câmera como último recurso
            if (playerPositions.Count == 0)
            {
                Debug.LogError("EnemySpawner: Still no players! Using camera fallback (host position).");
                if (mainCamera != null)
                {
                    return GetSpawnPosition3D(side); // Fallback para câmera
                }
                return Vector3.zero;
            }
        }

        // Calcula centro de todos os players
        Vector3 playersCenter = GetPlayersCenter();

        // Calcula bounds que engloba todos os players
        Bounds playerBounds = new Bounds(playerPositions[0], Vector3.zero);
        foreach (Vector3 pos in playerPositions)
        {
            playerBounds.Encapsulate(pos);
        }

        // Expande bounds para garantir spawn fora da vista
        playerBounds.Expand(spawnBuffer * 2f);

        Vector3 spawnPoint = Vector3.zero;

        switch (side)
        {
            case SpawnSide.Left:
                spawnPoint = new Vector3(
                    playerBounds.min.x - spawnBuffer,
                    groundLevelY,
                    Random.Range(playerBounds.min.z, playerBounds.max.z)
                );
                break;

            case SpawnSide.Right:
                spawnPoint = new Vector3(
                    playerBounds.max.x + spawnBuffer,
                    groundLevelY,
                    Random.Range(playerBounds.min.z, playerBounds.max.z)
                );
                break;

            case SpawnSide.Top:
                spawnPoint = new Vector3(
                    Random.Range(playerBounds.min.x, playerBounds.max.x),
                    groundLevelY,
                    playerBounds.max.z + spawnBuffer
                );
                break;

            case SpawnSide.Bottom:
                spawnPoint = new Vector3(
                    Random.Range(playerBounds.min.x, playerBounds.max.x),
                    groundLevelY,
                    playerBounds.min.z - spawnBuffer
                );
                break;
        }

        return spawnPoint;
    }

    // NOVO: Verifica se posição é segura para TODOS os players
    private bool IsSpawnPositionSafe(Vector3 position)
    {
        // Verifica colisões físicas
        Collider[] colliders = Physics.OverlapSphere(position, spawnCollisionCheckRadius);
        if (colliders.Length > 0)
        {
            foreach (Collider col in colliders)
            {
                if (col.isTrigger) continue;
                return false;
            }
        }

        // Verifica distância de TODOS os players
        foreach (Vector3 playerPos in playerPositions)
        {
            float distance = Vector3.Distance(position, playerPos);
            if (distance < minDistanceFromPlayers)
            {
                return false;
            }
        }

        return true;
    }

    // Função auxiliar para encontrar pontos de interseção de forma robusta
    private Vector3 GetWorldPointOnPlane(Vector2 viewportCoord, Plane plane)
    {
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

    /// <summary>
    /// NOVO: Calcula o lado oposto baseado na posição de um inimigo que deu despawn
    /// </summary>
    private SpawnSide GetOppositeSide(Vector3 despawnedPosition)
    {
        if (playerPositions.Count == 0)
        {
            UpdatePlayerPositions();
        }

        if (playerPositions.Count == 0)
        {
            return (SpawnSide)Random.Range(0, 4); // Fallback aleatório
        }

        // Calcula centro dos players
        Vector3 playersCenter = GetPlayersCenter();

        // Calcula vetor do centro dos players para o inimigo que deu despawn
        Vector3 directionToDespawned = despawnedPosition - playersCenter;

        // Inverte a direção para encontrar o lado oposto
        Vector3 oppositeDirection = -directionToDespawned;

        // Determina qual lado é baseado na direção oposta
        float absX = Mathf.Abs(oppositeDirection.x);
        float absZ = Mathf.Abs(oppositeDirection.z);

        if (absX > absZ)
        {
            // Movimento mais horizontal
            return oppositeDirection.x > 0 ? SpawnSide.Right : SpawnSide.Left;
        }
        else
        {
            // Movimento mais vertical
            return oppositeDirection.z > 0 ? SpawnSide.Top : SpawnSide.Bottom;
        }
    }

    // ATUALIZADO: Balanceamento baseado em TODOS os players
    private SpawnSide ChooseBalancedSpawnSide()
    {
        SpawnSide fallback = (SpawnSide)Random.Range(0, 4);

        UpdatePlayerPositions();
        if (playerPositions.Count == 0)
        {
            Debug.LogWarning("EnemySpawner: No players for ChooseBalancedSpawnSide, using random fallback.");
            return fallback;
        }

        EnemyStats[] activeEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        if (activeEnemies == null || activeEnemies.Length == 0) return fallback;

        // Calcula centro de todos os players
        Vector3 playersCenter = GetPlayersCenter();

        int leftCount = 0;
        int rightCount = 0;
        int topCount = 0;
        int bottomCount = 0;

        foreach (EnemyStats enemy in activeEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;

            Vector3 dirToEnemy = enemy.transform.position - playersCenter;

            // Classifica baseado na direção relativa ao centro dos players
            if (Mathf.Abs(dirToEnemy.x) > Mathf.Abs(dirToEnemy.z))
            {
                if (dirToEnemy.x > 0) rightCount++;
                else leftCount++;
            }
            else
            {
                if (dirToEnemy.z > 0) topCount++;
                else bottomCount++;
            }
        }

        int totalVisible = leftCount + rightCount + topCount + bottomCount;
        if (totalVisible == 0) return fallback;

        // Balanceamento inteligente
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

    // OBSOLETO: Mantido para compatibilidade, mas agora usa SpawnReplacementEnemy
    public void RespawnEnemy(GameObject enemyToRespawn)
    {
        if (enemyToRespawn == null) return;
        SpawnReplacementEnemy(enemyToRespawn);
    }

    /// <summary>
    /// NOVO: Spawna um novo inimigo para substituir um que foi destruído
    /// </summary>
    public void SpawnReplacementEnemy(GameObject destroyedEnemy)
    {
        if (destroyedEnemy == null)
        {
            Debug.LogWarning("EnemySpawner: Cannot spawn replacement for null enemy.");
            return;
        }

        // Apenas o servidor spawna inimigos
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        // Guarda a posição do inimigo destruído para calcular direção oposta
        Vector3 destroyedPosition = destroyedEnemy.transform.position;

        // Tenta obter o prefab do inimigo destruído
        GameObject prefabToSpawn = null;

        // Procura o prefab nas waves baseado no nome do inimigo
        string enemyName = destroyedEnemy.name.Replace("(Clone)", "").Trim();

        foreach (Wave wave in waves)
        {
            foreach (WaveEnemy waveEnemy in wave.enemies)
            {
                if (waveEnemy.enemyPrefab != null && waveEnemy.enemyPrefab.name == enemyName)
                {
                    prefabToSpawn = waveEnemy.enemyPrefab;
                    break;
                }
            }
            if (prefabToSpawn != null) break;
        }

        // Se não encontrou, pega um aleatório da wave atual
        if (prefabToSpawn == null && waves != null && waveIndex < waves.Count)
        {
            Wave currentWave = waves[waveIndex];
            if (currentWave.enemies.Count > 0)
            {
                int randomIndex = Random.Range(0, currentWave.enemies.Count);
                prefabToSpawn = currentWave.enemies[randomIndex].enemyPrefab;
            }
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("EnemySpawner: Could not find prefab to spawn replacement enemy!");
            return;
        }

        // NOVO: Calcula direção oposta ao despawn
        UpdatePlayerPositions();
        SpawnSide oppositeSpawnSide = GetOppositeSide(destroyedPosition);

        Vector3 spawnPos = Vector3.zero;
        bool foundSafePosition = false;

        // Tenta spawnar no lado oposto primeiro
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePos = GetSpawnPositionForAllPlayers(oppositeSpawnSide);

            if (IsSpawnPositionSafe(candidatePos))
            {
                spawnPos = candidatePos;
                foundSafePosition = true;
                if (showReplacementLogs)
                {
                    Debug.Log($"EnemySpawner: Spawning replacement on opposite side ({oppositeSpawnSide}) from despawn at {destroyedPosition}. New position: {spawnPos}");
                }
                break;
            }
        }

        // Se não conseguiu no lado oposto, tenta qualquer lado
        if (!foundSafePosition)
        {
            Debug.LogWarning("EnemySpawner: Could not spawn on opposite side, trying balanced spawn.");
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                SpawnSide spawnSide = ChooseBalancedSpawnSide();
                Vector3 candidatePos = GetSpawnPositionForAllPlayers(spawnSide);

                if (IsSpawnPositionSafe(candidatePos))
                {
                    spawnPos = candidatePos;
                    foundSafePosition = true;
                    break;
                }
            }
        }

        // Último recurso: força spawn no lado oposto
        if (!foundSafePosition)
        {
            spawnPos = GetSpawnPositionForAllPlayers(oppositeSpawnSide);
        }

        // Spawna o novo inimigo
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            RuntimeNetworkPrefabRegistry.TryRegister(prefabToSpawn);
            GameObject spawned = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            var netObj = spawned.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                netObj = spawned.AddComponent<NetworkObject>();
            }
            netObj.Spawn(true);

            // Fade-in (adiciona componente que funciona em todos os clientes)
            var fadeEffect = spawned.AddComponent<EnemyFadeEffect>();
            fadeEffect.StartFadeIn(fadeInDuration);

            // Apply global midgame mutation to replacements as well (server-side)
            if (GameManager.Instance != null && GameManager.Instance.GetGlobalMidgameMutation() != MutationType.None)
            {
                var es = spawned.GetComponent<EnemyStats>();
                if (es != null)
                {
                    GameManager.Instance.ApplyMidgameMutationToEnemy(es);
                }
            }
        }
        else
        {
            GameObject spawned = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            var fadeEffect = spawned.AddComponent<EnemyFadeEffect>();
            fadeEffect.StartFadeIn(fadeInDuration);
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