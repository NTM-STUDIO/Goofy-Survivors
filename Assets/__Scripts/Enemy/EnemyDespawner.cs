using UnityEngine;
using System.Collections;

public class EnemyDespawner : MonoBehaviour
{
    [Header("Despawn Settings")]
    [Tooltip("The radius around the player. Enemies outside this radius will be removed.")]
    [SerializeField] private float despawnRadius = 50f;
    [Tooltip("How often (in seconds) to check for enemies to remove.")]
    [SerializeField] private float checkInterval = 2f;

    [Header("Gizmo Settings")]
    [SerializeField] private bool showGizmo = true;

    // Internal References
    private Transform playerTransform; // This will be given to us by the GameManager
    [SerializeField] private EnemySpawner enemySpawner;

    /// <summary>
    /// The GameManager calls this and provides the newly spawned player object.
    /// </summary>
    public void Initialize(GameObject playerObject)
    {
        // If running under Netcode and the network is active, only the server should run despawner logic.
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening && !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            // disable this component on clients to avoid local-only despawning.
            enabled = false;
            return;
        }

        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
        else
        {
            Debug.LogError("FATAL ERROR: EnemyDespawner received a null player object! Despawner will not work.", this);
            enabled = false;
            return;
        }


        if (enemySpawner == null)
        {
            Debug.LogError("FATAL ERROR: EnemyDespawner could not find the EnemySpawner in the scene!", this);
            enabled = false;
            return;
        }

        // Only start the core logic after a successful initialization.
        StartCoroutine(DespawnEnemiesCoroutine());
        Debug.Log("EnemyDespawner Initialized successfully.");
    }

    IEnumerator DespawnEnemiesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            DespawnFarEnemies();
        }
    }

    void DespawnFarEnemies()
    {
        if (playerTransform == null || enemySpawner == null) return;

        EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        foreach (EnemyStats enemy in allEnemies)
        {
            // Check if enemy is far from ALL players (multiplayer support)
            if (IsEnemyFarFromAllPlayers(enemy.transform.position))
            {
                enemySpawner.RespawnEnemy(enemy.gameObject);
            }
        }
    }

    /// <summary>
    /// Checks if an enemy is outside the despawn radius of ALL players.
    /// In multiplayer, only despawn if the enemy is far from EVERY player.
    /// </summary>
    private bool IsEnemyFarFromAllPlayers(Vector3 enemyPosition)
    {
        // Check if we're in multiplayer mode
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            Unity.Netcode.NetworkManager.Singleton.IsListening && 
            Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            // Multiplayer: Check all connected players
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                
                float distance = Vector3.Distance(client.PlayerObject.transform.position, enemyPosition);
                
                // If ANY player is within range, don't despawn
                if (distance <= despawnRadius)
                {
                    return false;
                }
            }
            
            // All players are out of range, can despawn
            return true;
        }
        else
        {
            // Single player: Check only the local player
            if (playerTransform != null)
            {
                return Vector3.Distance(playerTransform.position, enemyPosition) > despawnRadius;
            }
            
            return false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        // Check if we're in multiplayer mode with active network
        if (Unity.Netcode.NetworkManager.Singleton != null && 
            Unity.Netcode.NetworkManager.Singleton.IsListening && 
            Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            // Draw despawn radius for all connected players
            Gizmos.color = Color.red;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject != null)
                {
                    Gizmos.DrawWireSphere(client.PlayerObject.transform.position, despawnRadius);
                }
            }
        }
        else
        {
            // Single player mode: Try to find the player in the editor for gizmo drawing
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }
            
            if (playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(playerTransform.position, despawnRadius);
            }
        }
    }
}