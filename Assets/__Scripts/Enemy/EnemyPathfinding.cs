using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics; // Required for Conditional attribute

[RequireComponent(typeof(EnemyMovement))]
public class EnemyPathfinding : MonoBehaviour
{
    [Header("Pathfinding Settings")]
    [SerializeField] private float pathUpdateRate = 0.5f;
    [SerializeField] private float waypointReachedDistance = 0.5f;

    private List<Vector3> path;
    private int currentWaypointIndex;
    private EnemyMovement movement;
    private Transform player;
    private float nextPathUpdate;
    private float waypointReachedSqrDistance;

    public Vector3? TargetOverride { get; set; }

    void Start()
    {
        movement = GetComponent<EnemyMovement>();
        if (movement == null)
        {
            LogError("EnemyMovement component not found!");
            enabled = false;
            return;
        }

        if (Pathfinding.Instance == null)
        {
            LogError("Pathfinding system not found in scene!");
            enabled = false;
            return;
        }

        path = new List<Vector3>();
        // PERFORMANCE: Pre-calculate the squared distance to avoid expensive Sqrt() calls in FixedUpdate.
        waypointReachedSqrDistance = waypointReachedDistance * waypointReachedDistance;
        
        FindPlayer();
        
        UpdatePath();
        nextPathUpdate = Time.time + pathUpdateRate;
        
        Log("Pathfinding is ready!");
    }

    void Update()
    {
        // Periodically check for the player if we don't have a target.
        // This is less frequent than every frame to save performance.
        if (player == null && Time.frameCount % 30 == 0)
        {
            FindPlayer();
        }

        if (Time.time >= nextPathUpdate)
        {
            UpdatePath();
            nextPathUpdate = Time.time + pathUpdateRate;
        }
    }

    void FixedUpdate()
    {
        FollowPath();
    }

    /// <summary>
    /// FIXED: Finds the player using the built-in tag system.
    /// This removes the dependency on the 'ActivePlayers' static list.
    /// </summary>
    private void FindPlayer()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            Log("Player found!");
        }
        else
        {
            // Log a warning only once to avoid spamming the console.
            if (player != null) 
            {
                Log("Player lost. Will attempt to find again.");
            }
            player = null;
        }
    }

    private void UpdatePath()
    {
        Vector3? targetPosition = TargetOverride.HasValue ? TargetOverride.Value : (player != null ? player.position : (Vector3?)null);

        if (!targetPosition.HasValue || Pathfinding.Instance == null) return;
        
        var newPath = Pathfinding.Instance.FindPath(transform.position, targetPosition.Value);
        if (newPath != null && newPath.Count > 0)
        {
            path = newPath;
            currentWaypointIndex = 0;
        }
    }

    private void FollowPath()
    {
        if (path == null || currentWaypointIndex >= path.Count) return;

        Vector3 currentWaypoint = path[currentWaypointIndex];
        // PERFORMANCE: Use squared magnitude to avoid expensive square root calculation.
        float sqrDistanceToWaypoint = (currentWaypoint - transform.position).sqrMagnitude;
        
        if (sqrDistanceToWaypoint < waypointReachedSqrDistance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= path.Count) return;
            currentWaypoint = path[currentWaypointIndex];
        }

        Vector3 direction = (currentWaypoint - transform.position).normalized;
        direction.y = 0;
        
        movement.TargetDirection = direction;
        
        #if UNITY_EDITOR
        DrawPath();
        #endif
    }

    #if UNITY_EDITOR
    private void DrawPath()
    {
        if (path == null || path.Count == 0) return;
        for (int i = 0; i < path.Count - 1; i++)
        {
            UnityEngine.Debug.DrawLine(path[i], path[i+1], Color.green);
        }
        if(currentWaypointIndex < path.Count)
        {
            UnityEngine.Debug.DrawLine(transform.position, path[currentWaypointIndex], Color.yellow);
        }
    }
    #endif

    // PERFORMANCE: These methods will only be compiled in the Unity Editor,
    // removing all logging overhead from final builds.
    [Conditional("UNITY_EDITOR")]
    private void Log(string message)
    {
        UnityEngine.Debug.Log($"[{gameObject.name}] PATHFINDING: {message}", this);
    }
    
    [Conditional("UNITY_EDITOR")]
    private void LogError(string message)
    {
        UnityEngine.Debug.LogError($"[{gameObject.name}] PATHFINDING: {message}", this);
    }
}