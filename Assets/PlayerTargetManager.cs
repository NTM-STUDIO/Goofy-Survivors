using UnityEngine;
using Unity.Netcode;

/// <summary>
/// A highly performant, centralized manager to find and cache the closest player transform.
/// Other scripts should ask this manager for the player instead of searching themselves.
/// </summary>
public class PlayerTargetManager : MonoBehaviour
{
    // Singleton instance to be accessed by any script.
    public static PlayerTargetManager Instance { get; private set; }

    [Tooltip("How often the manager scans for the closest player. Higher values are more performant.")]
    [SerializeField] private float searchInterval = 1f; // Search once per second.

    // The public property that all other scripts will access.
    public Transform ClosestPlayer { get; private set; }

    private float searchTimer;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            ClosestPlayer = FindClosestActivePlayer();
            searchTimer = searchInterval;
        }
    }

    private Transform FindClosestActivePlayer()
    {
        Transform closest = null;
        float minSqrDist = float.MaxValue;
        Vector3 currentPosition = transform.position; // Position doesn't matter much for a global finder

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            // Networked search
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                var ps = client.PlayerObject.GetComponent<PlayerStats>();
                if (ps == null || ps.IsDowned) continue;

                float sqrDist = (ps.transform.position - currentPosition).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = ps.transform;
                }
            }
        }
        else
        {
            // Single-player search
            var allPlayers = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            foreach (var ps in allPlayers)
            {
                if (ps == null || ps.IsDowned) continue;
                float sqrDist = (ps.transform.position - currentPosition).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closest = ps.transform;
                }
            }
        }
        return closest;
    }
}