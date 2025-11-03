using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Spawns NON-NETWORKED local-only prefabs on each client using a stable per-client random seed.
/// - Server sets a shared session seed (GameManager.SessionSeed)
/// - Each client derives: clientSeed = SessionSeed ^ (LocalClientId * largePrime) ^ additionalSalt
/// - Objects spawned here MUST NOT have NetworkObject components
/// - Safe to place multiple in a scene; each spawner can have its own AdditionalSalt
/// </summary>
public class ClientOnlyRandomSpawner : MonoBehaviour
{
    [Header("Prefabs (no NetworkObject)")]
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();

    [Header("Spawn Settings")] 
    [Tooltip("How many objects to spawn locally on this client")] 
    [SerializeField] private int count = 10;
    [Tooltip("Extents in X/Z around this GameObject where objects may spawn (Y uses this transform's Y)")]
    [SerializeField] private Vector2 areaExtentsXZ = new Vector2(10, 10);
    [Tooltip("Optional extra salt to make this spawner's sequence different from others")]
    [SerializeField] private int additionalSalt = 0;
    [Tooltip("Also run on the host/server's local client (true) or only on pure clients (false)")]
    [SerializeField] private bool includeServerHost = true;

    [Header("Cleanup")] 
    [Tooltip("If true, spawned objects will be destroyed when this component is disabled/destroyed")]
    [SerializeField] private bool cleanupOnDestroy = true;

    private readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        // Single-player: behave like a normal local spawner
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            SpawnLocal(new System.Random(ComposeSeed(0UL)));
            return;
        }

        // Multiplayer: decide if we run on this machine
        bool isServer = nm.IsServer;
        if (isServer && !includeServerHost)
        {
            // Skip on server if configured not to generate on host
            return;
        }

        // Derive a stable per-client seed for this session
        ulong clientId = nm.LocalClient != null ? nm.LocalClient.ClientId : 0UL;
        int seed = ComposeSeed(clientId);
        SpawnLocal(new System.Random(seed));
    }

    private int ComposeSeed(ulong clientId)
    {
        int sessionSeed = 0;
        var gm = GameManager.Instance;
        if (gm != null) sessionSeed = gm.SessionSeed;
        unchecked
        {
            int clientSalt = (int)(clientId * 73856093UL);
            int hash = sessionSeed ^ clientSalt ^ additionalSalt;
            if (hash == 0) hash = 1; // avoid zero seed for some RNGs
            return hash;
        }
    }

    private void SpawnLocal(System.Random rng)
    {
        if (prefabs == null || prefabs.Count == 0 || count <= 0) return;
        Vector3 basePos = transform.position;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[rng.Next(0, prefabs.Count)];
            if (prefab == null) continue;
            // Ensure prefab doesn't carry a NetworkObject
            if (prefab.GetComponent<NetworkObject>() != null)
            {
                Debug.LogWarning($"[ClientOnlyRandomSpawner] Prefab '{prefab.name}' has a NetworkObject; skip to avoid syncing.");
                continue;
            }
            float x = (float)(rng.NextDouble() * 2.0 - 1.0) * areaExtentsXZ.x;
            float z = (float)(rng.NextDouble() * 2.0 - 1.0) * areaExtentsXZ.y;
            Vector3 pos = new Vector3(basePos.x + x, basePos.y, basePos.z + z);
            Quaternion rot = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            GameObject go = Instantiate(prefab, pos, rot, this.transform);
            spawned.Add(go);
        }
    }

    void OnDisable()
    {
        if (!cleanupOnDestroy) return;
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }
}
