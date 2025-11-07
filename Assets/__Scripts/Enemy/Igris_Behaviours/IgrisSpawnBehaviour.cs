using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class IgrisSpawnBehaviour : MonoBehaviour
{
    public List<IgrisWave> waves = new List<IgrisWave>();
    [Tooltip("Índice da wave atual")]
    public int waveIndex = 0;
    [Tooltip("Começar automaticamente ao iniciar?")]
    [SerializeField] private bool spawnOnStart = false;
    [Tooltip("Escalar número de inimigos pelo nº de jogadores")]
    [SerializeField] private bool scaleWithPlayerCount = true;
    [Tooltip("Distância radial ao redor do jogador para spawn")]
    [SerializeField] private float spawnRadius = 10f;
    private bool isSpawning = false;
    private Coroutine spawnRoutine;

    [Header("Post-Spawn Behavior")]
    [SerializeField] private float watchDistance = 40f; // Distance Igris should keep from the player
    [SerializeField] private bool isWatching = false;
    [SerializeField] private Vector3 lastPlayerPosition;

    private EnemyPathfinding enemyPathfinding;
    private Transform targetPlayer;
    private readonly List<Transform> cachedPlayers = new List<Transform>();
    private readonly List<GameObject> activeMinions = new List<GameObject>();

    void Start()
    {
        enemyPathfinding = GetComponent<EnemyPathfinding>();
        if (spawnOnStart)
        {
            StartSpawning();
        }
    }

    private void OnValidate()
    {
        if (waves == null) waves = new List<IgrisWave>();
        // Provide a default entry to make it obvious in Inspector
        if (waves.Count == 0)
        {
            waves.Add(new IgrisWave
            {
                waveName = "Wave 1",
                enemies = new List<IgrisWaveEnemy>()
                {
                    new IgrisWaveEnemy{ enemyPrefab = null, enemyCount = 5 }
                },
                timeUntilNextWave = 10f,
                spawnInterval = 0.5f
            });
        }
    }

    void Update()
    {
        bool minionsAlive = HasActiveMinions();

        if (minionsAlive)
        {
            EnsureWatchMode();
            MaintainWatchDistance();
        }
        else
        {
            if (isWatching)
            {
                StopWatching();
            }
            FindAndFollowPlayer();
        }
    }

    public void StartSpawning()
    {
        if (waves == null || waves.Count == 0)
        {
            Debug.LogWarning("[Igris] Nenhuma wave configurada.");
            return;
        }
        if (!isSpawning)
        {
            Debug.Log("[Igris] Iniciando sequência de waves...");
            isWatching = false;
            waveIndex = 0;
            spawnRoutine = StartCoroutine(SpawnWaves());
        }
    }

    // Exposed in Inspector for quick testing
    [ContextMenu("Igris/Start Spawning Waves")]
    private void ContextStartSpawning()
    {
        StartSpawning();
    }

    private int GetPlayerCountMultiplier()
    {
        if (!scaleWithPlayerCount) return 1;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            return Mathf.Max(1, playerCount);
        }
        return 1; // single-player ou não-server
    }

    void FindAndFollowPlayer()
    {
        // Simple logic to find the closest player, you might have a better system for this
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float minDistance = float.MaxValue;
        GameObject closestPlayer = null;

        foreach (var player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPlayer = player;
            }
        }

        if (closestPlayer != null)
        {
            targetPlayer = closestPlayer.transform;
            enemyPathfinding.TargetOverride = null; // Follow player
        }
    }

    IEnumerator SpawnWaves()
    {
        isSpawning = true;
        yield return null; // Garante consistência com EnemySpawner
        while (waveIndex < waves.Count)
        {
            IgrisWave currentWave = waves[waveIndex];
            if (currentWave == null)
            {
                waveIndex++;
                continue;
            }

            List<IgrisWaveEnemy> enemies = currentWave.enemies;
            if (enemies == null)
            {
                enemies = new List<IgrisWaveEnemy>();
                currentWave.enemies = enemies;
            }

            string waveName = string.IsNullOrEmpty(currentWave.waveName) ? (waveIndex + 1).ToString() : currentWave.waveName;
            Debug.Log($"[Igris] Wave {waveName} iniciada.");

            // Preparar contagens (escalar por número de jogadores se habilitado)
            List<int> remainingCounts = new List<int>(enemies.Count);
            int totalEnemiesToSpawn = 0;
            int playerMultiplier = GetPlayerCountMultiplier();
            if (enemies.Count > 0)
            {
                foreach (var waveEnemy in enemies)
                {
                    if (waveEnemy == null || waveEnemy.enemyPrefab == null)
                    {
                        remainingCounts.Add(0);
                        continue;
                    }

                    int scaled = Mathf.Max(0, waveEnemy.enemyCount * playerMultiplier);
                    remainingCounts.Add(scaled);
                    totalEnemiesToSpawn += scaled;
                }
            }

            if (totalEnemiesToSpawn == 0)
            {
                Debug.LogWarning($"[Igris] Wave {waveName} sem inimigos válidos.");
            }

            while (totalEnemiesToSpawn > 0)
            {
                int roll = Random.Range(0, totalEnemiesToSpawn);
                int cumulative = 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (remainingCounts[i] == 0) continue;
                    cumulative += remainingCounts[i];
                    if (roll < cumulative)
                    {
                        var selected = enemies[i];
                        if (selected == null || selected.enemyPrefab == null)
                        {
                            Debug.LogWarning($"[Igris] Wave {waveName} com inimigo inválido no índice {i}.");
                            remainingCounts[i]--;
                            totalEnemiesToSpawn--;
                            break;
                        }
                        Transform chosenPlayer = GetRandomPlayer();
                        if (chosenPlayer == null)
                        {
                            Debug.LogWarning("[Igris] Nenhum jogador disponível. Repetindo tentativa de spawn em 1s.");
                            yield return new WaitForSeconds(1f);
                            break;
                        }
                        SpawnSingleEnemyAroundPlayer(selected.enemyPrefab, chosenPlayer);
                        remainingCounts[i]--;
                        totalEnemiesToSpawn--;
                        if (currentWave.spawnInterval > 0f)
                        {
                            yield return new WaitForSeconds(currentWave.spawnInterval);
                        }
                        else
                        {
                            yield return null;
                        }
                        break;
                    }
                }
            }

            if (currentWave.timeUntilNextWave > 0f)
            {
                yield return new WaitForSeconds(currentWave.timeUntilNextWave);
            }
            else
            {
                yield return null;
            }
            waveIndex++;
        }

        Debug.Log("[Igris] Todas as waves concluídas.");
        isSpawning = false;
        spawnRoutine = null;
        if (HasActiveMinions())
        {
            EnsureWatchMode();
        }
        else if (isWatching)
        {
            StopWatching();
        }
    }

    private void SpawnSingleEnemyAroundPlayer(GameObject prefab, Transform player)
    {
        if (prefab == null || player == null) return;
        float angle = Random.value * Mathf.PI * 2f;
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        Vector3 spawnPos = player.position + dir * spawnRadius;

        // Validar nó walkable se existir sistema de grid
        bool canSpawn = true;
        if (Pathfinding.Instance != null)
        {
            var grid = Pathfinding.Instance.GetComponent<Grid>();
            if (grid != null && !grid.NodeFromWorldPoint(spawnPos).walkable)
            {
                // Tentar algumas variações de ângulo
                canSpawn = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    angle = Random.value * Mathf.PI * 2f;
                    dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    spawnPos = player.position + dir * spawnRadius;
                    if (grid.NodeFromWorldPoint(spawnPos).walkable) { canSpawn = true; break; }
                }
            }
        }

        if (!canSpawn)
        {
            Debug.LogWarning("[Igris] Não encontrou posição walkable para inimigo após tentativas.");
            return;
        }

        // Network vs Single-player
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            RuntimeNetworkPrefabRegistry.TryRegister(prefab);
            GameObject spawned = Instantiate(prefab, spawnPos, Quaternion.identity);
            var netObj = spawned.GetComponent<NetworkObject>();
            if (netObj == null) netObj = spawned.AddComponent<NetworkObject>();
            netObj.Spawn(true);
            TrackMinion(spawned);
        }
        else
        {
            GameObject spawned = Instantiate(prefab, spawnPos, Quaternion.identity);
            TrackMinion(spawned);
        }
    }

    void MaintainWatchDistance()
    {
        if (targetPlayer == null)
        {
            StopWatching(); // No player to watch, go back to default behavior
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // Check if the player has moved significantly, or if Igris is outside the watch distance
        if (Vector3.Distance(lastPlayerPosition, targetPlayer.position) > 0.1f || Mathf.Abs(distanceToPlayer - watchDistance) > 1f)
        {
            Vector3 direction = (transform.position - targetPlayer.position).normalized;
            Vector3 targetPosition = targetPlayer.position + direction * watchDistance;

            // Use pathfinding to move to the watch position
            enemyPathfinding.TargetOverride = targetPosition;
            lastPlayerPosition = targetPlayer.position; // Update last known player position
        }
        else
        {
            // Player is stationary and Igris is at the right distance, so stop moving.
            enemyPathfinding.TargetOverride = transform.position;
        }
    }

    void StopWatching()
    {
        isWatching = false;
        if (enemyPathfinding != null)
        {
            enemyPathfinding.TargetOverride = null;
        }
        targetPlayer = null;
    }

    private Transform GetRandomPlayer()
    {
        var players = CollectActivePlayers();
        if (players.Count == 0) return null;
        return players[Random.Range(0, players.Count)];
    }

    private List<Transform> CollectActivePlayers()
    {
        cachedPlayers.Clear();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObj in players)
        {
            if (playerObj != null)
            {
                cachedPlayers.Add(playerObj.transform);
            }
        }
        return cachedPlayers;
    }

    private void EnsureWatchMode()
    {
        if (isWatching && targetPlayer != null)
        {
            return;
        }

        var players = CollectActivePlayers();
        if (players.Count == 0)
        {
            StopWatching();
            return;
        }

        isWatching = true;
        targetPlayer = players[0];
        lastPlayerPosition = targetPlayer.position;
    }

    private void TrackMinion(GameObject spawned)
    {
        if (spawned == null) return;
        if (!activeMinions.Contains(spawned))
        {
            activeMinions.Add(spawned);
            EnsureWatchMode();
        }
    }

    private bool HasActiveMinions()
    {
        bool foundActive = false;
        for (int i = activeMinions.Count - 1; i >= 0; i--)
        {
            GameObject minion = activeMinions[i];
            if (minion == null || !minion.activeInHierarchy)
            {
                activeMinions.RemoveAt(i);
                continue;
            }
            foundActive = true;
        }
        return foundActive;
    }
}

[System.Serializable]
public class IgrisWaveEnemy
{
    public GameObject enemyPrefab;
    public int enemyCount = 1;
}

[System.Serializable]
public class IgrisWave
{
    public string waveName = "Wave";
    public List<IgrisWaveEnemy> enemies = new List<IgrisWaveEnemy>();
    public float timeUntilNextWave = 10f;
    public float spawnInterval = 0.5f;
}

#if UNITY_EDITOR
[CustomEditor(typeof(IgrisSpawnBehaviour))]
public class IgrisSpawnBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Wave Configuration", EditorStyles.boldLabel);
        SerializedProperty wavesList = serializedObject.FindProperty("waves");
        EditorGUILayout.PropertyField(wavesList, true);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnOnStart"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scaleWithPlayerCount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnRadius"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Post-Spawn Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("watchDistance"));

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Add New Wave"))
        {
            int newIndex = wavesList.arraySize;
            wavesList.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newWave = wavesList.GetArrayElementAtIndex(newIndex);
            newWave.FindPropertyRelative("waveName").stringValue = $"Wave {newIndex + 1}";
            newWave.FindPropertyRelative("timeUntilNextWave").floatValue = 10f;
            newWave.FindPropertyRelative("spawnInterval").floatValue = 0.5f;
            SerializedProperty enemiesProp = newWave.FindPropertyRelative("enemies");
            enemiesProp.arraySize = 0;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
