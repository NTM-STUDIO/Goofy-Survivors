using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : NetworkBehaviour
{
    [System.Serializable]
    public class AssetData
    {
        public GameObject assetPrefab;
        [Tooltip("Higher value = more frequent")]
        public float weight = 1f;
    }

    [Header("Important Items (Networked - Low Density)")]
    [Tooltip("Items like Chests, Pickups. Spawned everywhere at start (Server Only).")]
    public AssetData[] networkedAssets;
    public float networkedDensity = 0.0005f; // Keep VERY low

    [Header("Decorations (Local - High Density)")]
    [Tooltip("Visuals like Grass, Rocks. Spawned only near player.")]
    public AssetData[] decorationAssets;
    public float decorationDensity = 0.05f; // Can be higher now!

    [Header("Map Settings")]
    public int mapWidth = 2000;
    public int mapHeight = 1200;
    
    [Header("Chunk System (Lag Prevention)")]
    [Tooltip("Size of each loading square. 30-50 is usually good.")]
    public int chunkSize = 40;
    [Tooltip("How many chunks around the player are visible? 1 = 3x3 grid, 2 = 5x5 grid.")]
    public int viewDistance = 1;

    // --- INTERNAL DATA ---
    // Stores DATA about where decorations should be, without spawning them
    private Dictionary<Vector2Int, List<DecorationInfo>> virtualMap = new Dictionary<Vector2Int, List<DecorationInfo>>();
    // Stores ACTUAL GameObjects currently visible
    private Dictionary<Vector2Int, List<GameObject>> activeChunks = new Dictionary<Vector2Int, List<GameObject>>();
    
    private struct DecorationInfo
    {
        public Vector3 position;
        public int prefabIndex;
        // Rotation/Scale could be added here for variety
    }

    private bool isMapReady = false;
    private Vector2Int lastPlayerChunk = new Vector2Int(-999, -999);
    private Transform localPlayerTransform;

    public void GenerateMap()
    {
        if (isMapReady) return;

        // 1. Sync Seed
        if (GameManager.Instance != null)
        {
            Random.InitState(GameManager.Instance.SessionSeed);
        }

        // 2. Spawn Networked Items (Global, Permanent)
        // Only Server spawns these because they have NetworkObject
        if (NetworkManager.Singleton.IsServer || !GameManager.Instance.isP2P)
        {
            SpawnNetworkedGlobalItems();
        }

        // 3. Generate Virtual Decoration Map (Local, Everyone does this)
        // This is fast because we just save coordinates, we don't Instantiate yet.
        GenerateVirtualDecorations();

        isMapReady = true;
        Debug.Log($"[MapGenerator] Map Generated. Virtual Chunks: {virtualMap.Count}");
    }

    private void SpawnNetworkedGlobalItems()
    {
        if (networkedAssets == null || networkedAssets.Length == 0) return;

        float totalWeight = networkedAssets.Sum(a => a.weight);
        int count = Mathf.RoundToInt(mapWidth * mapHeight * networkedDensity);
        
        // Safety cap for networked items
        count = Mathf.Min(count, 1000); 

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetRandomPos();
            AssetData asset = GetRandomAsset(networkedAssets, totalWeight);
            
            if (asset != null && asset.assetPrefab != null)
            {
                GameObject obj = Instantiate(asset.assetPrefab, pos, Quaternion.identity);
                var netObj = obj.GetComponent<NetworkObject>();
                if (netObj != null && GameManager.Instance.isP2P)
                {
                    netObj.Spawn(true);
                }
            }
        }
        Debug.Log($"[MapGenerator] Spawned {count} Networked Items.");
    }

    private void GenerateVirtualDecorations()
    {
        if (decorationAssets == null || decorationAssets.Length == 0) return;

        float totalWeight = decorationAssets.Sum(a => a.weight);
        int count = Mathf.RoundToInt(mapWidth * mapHeight * decorationDensity);

        Debug.Log($"[MapGenerator] Calculating {count} decorations (Virtual)...");

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetRandomPos();
            
            // Determine which chunk this position belongs to
            Vector2Int chunkCoord = GetChunkCoordinate(pos);

            // Select prefab index
            int prefabIndex = GetRandomAssetIndex(decorationAssets, totalWeight);

            // Add to virtual data
            if (!virtualMap.ContainsKey(chunkCoord))
            {
                virtualMap[chunkCoord] = new List<DecorationInfo>();
            }

            virtualMap[chunkCoord].Add(new DecorationInfo 
            { 
                position = pos, 
                prefabIndex = prefabIndex 
            });
        }
    }

    private void Update()
    {
        if (!isMapReady) return;

        // Find local player if lost
        if (localPlayerTransform == null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
            {
                localPlayerTransform = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            }
            else
            {
                // Fallback for SP
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) localPlayerTransform = p.transform;
            }
            return;
        }

        UpdateChunks();
    }

    private void UpdateChunks()
    {
        Vector3 playerPos = localPlayerTransform.position;
        Vector2Int currentChunk = GetChunkCoordinate(playerPos);

        // Only update if we moved to a new chunk
        if (currentChunk == lastPlayerChunk) return;
        lastPlayerChunk = currentChunk;

        // 1. Identify valid chunks around player
        List<Vector2Int> chunksToKeep = new List<Vector2Int>();

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                chunksToKeep.Add(new Vector2Int(currentChunk.x + x, currentChunk.y + y));
            }
        }

        // 2. Unload far away chunks
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        foreach (var chunk in activeChunks.Keys)
        {
            if (!chunksToKeep.Contains(chunk))
            {
                chunksToRemove.Add(chunk);
            }
        }

        foreach (var chunk in chunksToRemove)
        {
            UnloadChunk(chunk);
        }

        // 3. Load new nearby chunks
        foreach (var chunk in chunksToKeep)
        {
            LoadChunk(chunk);
        }
    }

    private void LoadChunk(Vector2Int chunkCoord)
    {
        // If already active or no data exists, skip
        if (activeChunks.ContainsKey(chunkCoord)) return;
        if (!virtualMap.ContainsKey(chunkCoord)) return;

        List<GameObject> spawnedObjects = new List<GameObject>();
        List<DecorationInfo> dataList = virtualMap[chunkCoord];

        // Create a parent container for tidiness
        GameObject chunkParent = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
        chunkParent.transform.SetParent(transform);

        foreach (var info in dataList)
        {
            GameObject prefab = decorationAssets[info.prefabIndex].assetPrefab;
            // Spawn visual only (NO NetworkObject here!)
            GameObject instance = Instantiate(prefab, info.position, Quaternion.identity, chunkParent.transform);
            
            // Optimization: Static batching for performance
            instance.isStatic = true; 
            
            spawnedObjects.Add(instance);
        }

        // Add parent to list too so we can destroy it easily
        spawnedObjects.Add(chunkParent);
        
        activeChunks.Add(chunkCoord, spawnedObjects);
    }

    private void UnloadChunk(Vector2Int chunkCoord)
    {
        if (activeChunks.TryGetValue(chunkCoord, out List<GameObject> objects))
        {
            foreach (var obj in objects)
            {
                Destroy(obj);
            }
            activeChunks.Remove(chunkCoord);
        }
    }

    public void ClearMap()
    {
        isMapReady = false;
        virtualMap.Clear();
        foreach (var chunkList in activeChunks.Values)
        {
            foreach (var obj in chunkList) Destroy(obj);
        }
        activeChunks.Clear();
        lastPlayerChunk = new Vector2Int(-999, -999);

        // Clear children
        foreach(Transform child in transform) Destroy(child.gameObject);
    }

    // --- HELPERS ---

    private Vector2Int GetChunkCoordinate(Vector3 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize)
        );
    }

    private Vector3 GetRandomPos()
    {
        return new Vector3(
            Random.Range(-mapWidth / 2f, mapWidth / 2f),
            0f,
            Random.Range(-mapHeight / 2f, mapHeight / 2f)
        );
    }

    private AssetData GetRandomAsset(AssetData[] list, float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var asset in list)
        {
            cumulative += asset.weight;
            if (roll <= cumulative) return asset;
        }
        return list[0];
    }

    private int GetRandomAssetIndex(AssetData[] list, float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for(int i=0; i<list.Length; i++)
        {
            cumulative += list[i].weight;
            if (roll <= cumulative) return i;
        }
        return 0;
    }
}