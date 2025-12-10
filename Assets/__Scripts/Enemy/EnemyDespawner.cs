using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Despawns enemies that are too far from all players.
/// Uses radius-based detection only (no camera checks).
/// </summary>
public class EnemyDespawner : MonoBehaviour
{
    [Header("Despawn Settings")]
    [Tooltip("The radius around the player. Enemies outside this radius will be removed.")]
    [SerializeField] private float despawnRadius = 70f;
    [Tooltip("How often (in seconds) to check for enemies to remove.")]
    [SerializeField] private float checkInterval = 2f;
    
    [Header("Visual Settings")]
    [Tooltip("Duração do fade-out antes de despawn (0 = instantâneo)")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool showDebugLogs = false;

    // Internal References
    private Transform playerTransform;
    [SerializeField] private EnemySpawner enemySpawner;
    private HashSet<GameObject> despawningEnemies = new HashSet<GameObject>();

    /// <summary>
    /// Called by PlayerSpawnManager after the player is spawned.
    /// </summary>
    public void Initialize(GameObject playerObject)
    {
        // Only server runs despawn logic in multiplayer
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            Unity.Netcode.NetworkManager.Singleton.IsListening && 
            !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            return;
        }

        if (playerObject == null)
        {
            Debug.LogError("[EnemyDespawner] Received null player! Despawner disabled.", this);
            enabled = false;
            return;
        }

        playerTransform = playerObject.transform;

        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
            if (enemySpawner == null)
            {
                Debug.LogError("[EnemyDespawner] EnemySpawner not found! Despawner disabled.", this);
                enabled = false;
                return;
            }
        }

        StartCoroutine(DespawnLoop());
        Debug.Log("[EnemyDespawner] Initialized successfully.");
    }

    IEnumerator DespawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            CheckAndDespawnFarEnemies();
        }
    }

    void CheckAndDespawnFarEnemies()
    {
        if (playerTransform == null || enemySpawner == null) return;

        EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        int despawnedCount = 0;

        foreach (EnemyStats enemy in allEnemies)
        {
            if (enemy == null || despawningEnemies.Contains(enemy.gameObject)) continue;

            if (IsEnemyFarFromAllPlayers(enemy.transform.position))
            {
                despawnedCount++;
                StartCoroutine(DespawnEnemy(enemy.gameObject));
            }
        }

        if (showDebugLogs && despawnedCount > 0)
        {
            Debug.Log($"[EnemyDespawner] Despawned {despawnedCount} enemies outside radius ({despawnRadius})");
        }
    }

    /// <summary>
    /// Returns true if enemy is outside despawn radius of ALL players.
    /// </summary>
    private bool IsEnemyFarFromAllPlayers(Vector3 enemyPosition)
    {
        // Multiplayer: check all connected players
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            Unity.Netcode.NetworkManager.Singleton.IsListening && 
            Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                
                float distance = Vector3.Distance(client.PlayerObject.transform.position, enemyPosition);
                if (distance <= despawnRadius)
                {
                    return false; // At least one player is close
                }
            }
            return true; // All players are far
        }
        
        // Singleplayer: check local player
        if (playerTransform != null)
        {
            return Vector3.Distance(playerTransform.position, enemyPosition) > despawnRadius;
        }
        
        return false;
    }

    IEnumerator DespawnEnemy(GameObject enemy)
    {
        if (enemy == null) yield break;

        despawningEnemies.Add(enemy);

        // Optional fade-out
        if (fadeOutDuration > 0f)
        {
            SpriteRenderer[] sprites = enemy.GetComponentsInChildren<SpriteRenderer>();
            float elapsed = 0f;
            
            while (elapsed < fadeOutDuration && enemy != null)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / fadeOutDuration);
                
                foreach (var sprite in sprites)
                {
                    if (sprite != null)
                    {
                        Color c = sprite.color;
                        c.a = alpha;
                        sprite.color = c;
                    }
                }
                yield return null;
            }
        }

        despawningEnemies.Remove(enemy);

        if (enemy != null)
        {
            // Destroy (networked or local)
            if (Unity.Netcode.NetworkManager.Singleton != null && 
                Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                var netObj = enemy.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(enemy);
                }
            }
            else
            {
                Destroy(enemy);
            }

            // Spawn replacement
            if (enemySpawner != null)
            {
                enemySpawner.SpawnReplacementEnemy(enemy);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);

        // Multiplayer
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            Unity.Netcode.NetworkManager.Singleton.IsListening && 
            Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject != null)
                {
                    Gizmos.DrawWireSphere(client.PlayerObject.transform.position, despawnRadius);
                }
            }
        }
        // Singleplayer / Editor
        else
        {
            Transform target = playerTransform;
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
            
            if (target != null)
            {
                Gizmos.DrawWireSphere(target.position, despawnRadius);
            }
        }
    }
}