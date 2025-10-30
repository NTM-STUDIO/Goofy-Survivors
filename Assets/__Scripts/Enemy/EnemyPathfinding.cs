using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(EnemyMovement))]
public class EnemyPathfinding : MonoBehaviour
{
    [Header("Pathfinding Settings")]
    [Tooltip("How often (in seconds) should the enemy recalculate its path")]
    [SerializeField] private float pathUpdateRate = 0.5f;
    [Tooltip("How close the enemy needs to be to a waypoint to move to the next one")]
    [SerializeField] private float waypointReachedDistance = 0.5f;

    private List<Vector3> path;
    private int currentWaypointIndex;
    private EnemyMovement movement;
    private Transform player;
    private float nextPathUpdate;

    private void Start()
    {
        Debug.Log($"[{gameObject.name}] PATHFINDING: Initializing...", this);

        // In P2P mode the server is authoritative for enemy AI/pathfinding.
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.Log($"[{gameObject.name}] PATHFINDING: Disabled on client (server runs AI).", this);
                enabled = false;
                return;
            }
        }

        // Get required components
        movement = GetComponent<EnemyMovement>();
        if (movement == null)
        {
            Debug.LogError($"[{gameObject.name}] PATHFINDING: EnemyMovement component not found!", this);
            enabled = false;
            return;
        }

        // Attempt to find the player, but don't disable the script if not found yet.
        FindPlayer();

        if (Pathfinding.Instance == null)
        {
            Debug.LogError($"[{gameObject.name}] PATHFINDING: Pathfinding system not found in scene!", this);
            enabled = false;
            return;
        }

        // Initialize path
        path = new List<Vector3>();
        
        // Force an immediate path update
        UpdatePath();
        nextPathUpdate = Time.time + pathUpdateRate;
        
        Debug.Log($"[{gameObject.name}] PATHFINDING: Ready!", this);
    }

    private void Update()
    {
        // If player is not found, keep trying to find it.
        if (player == null)
        {
            FindPlayer();
            // If still not found, do nothing this frame.
            if (player == null) return;
        }

        // Only update path in Update, not movement
        if (Time.time >= nextPathUpdate)
        {
            UpdatePath();
            nextPathUpdate = Time.time + pathUpdateRate;
        }
    }

    private void FixedUpdate()
    {
        // Movement logic runs in FixedUpdate to sync with EnemyMovement
        if (player == null) return;
        
        FollowPath();
    }

    /// <summary>
    /// Finds the player GameObject in the scene and assigns its transform.
    /// </summary>
    private void FindPlayer()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            Debug.Log($"[{gameObject.name}] PATHFINDING: Player found!", this);
        }
    }

    private void UpdatePath()
    {
        if (Pathfinding.Instance == null)
        {
            Debug.LogError($"[{gameObject.name}] PATHFINDING: Instance is null!", this);
            return;
        }

        var newPath = Pathfinding.Instance.FindPath(transform.position, player.position);
        if (newPath == null || newPath.Count == 0)
        {
            return;
        }

        path = newPath;
        currentWaypointIndex = 0;
        Debug.Log($"[{gameObject.name}] PATHFINDING: New path with {path.Count} waypoints", this);
    }

    private void FollowPath()
    {
        // If we don't have a valid path, request one but don't spam updates
        if (path == null || path.Count == 0)
        {
            return; // Wait for next path update cycle
        }
        
        // Check if we've gone past the end of the path
        if (currentWaypointIndex >= path.Count)
        {
            return; // Wait for next path update cycle
        }

        // Debug draw the path
        #if UNITY_EDITOR
        // Draw the full path
        for (int i = 0; i < path.Count - 1; i++)
        {
            Debug.DrawLine(path[i], path[i + 1], Color.green);
        }
        
        // Highlight current waypoint
        Debug.DrawLine(transform.position, path[currentWaypointIndex], Color.yellow);
        #endif

        // Check distance to current waypoint
        float distanceToWaypoint = Vector3.Distance(transform.position, path[currentWaypointIndex]);
        
        // Move to next waypoint if we're close enough
        if (distanceToWaypoint < waypointReachedDistance)
        {
            currentWaypointIndex++;
            
            if (currentWaypointIndex >= path.Count)
            {
                return; // Wait for next path update
            }
        }

        // Calculate direction to current waypoint
        Vector3 toWaypoint = path[currentWaypointIndex] - transform.position;
        toWaypoint.y = 0; // Keep movement in XZ plane
        
        // Always update direction, even if close
        if (toWaypoint.sqrMagnitude > 0.001f)
        {
            Vector3 direction = toWaypoint.normalized;
            movement.TargetDirection = direction;
            
            #if UNITY_EDITOR
            // Visual debug - movement direction
            Debug.DrawRay(transform.position, direction * 2f, Color.red);
            #endif
        }
    }
}