using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    [System.Serializable]
    public class AssetData
    {
        public GameObject assetPrefab;
        [Tooltip("Higher value = more frequent")]
        public float weight = 1f;
        [Tooltip("Check if this object has a NetworkObject and needs to be synced (e.g., interactables). Uncheck for visual props (grass, rocks).")]
        public bool isNetworked = true;
    }

    [Header("Generation Settings")]
    public AssetData[] assets;
    public int mapWidth = 2000;
    public int mapHeight = 1200;
    
    [Tooltip("Assets per square unit. WARNING: Keep very low for large maps (e.g. 0.001).")]
    public float assetDensity = 0.002f; 

    [Tooltip("Safety limit to prevent freezing the game.")]
    public int maxObjectLimit = 5000; 

    private bool hasGenerated = false;

    public void GenerateMap()
    {
        if (hasGenerated) return;

        // 1. Validations
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[MapGenerator] No assets assigned in Inspector!");
            return;
        }

        // 2. Sync Random Seed (Crucial for Multiplayer P2P)
        if (GameManager.Instance != null)
        {
            int seed = GameManager.Instance.SessionSeed;
            Random.InitState(seed);
            Debug.Log($"[MapGenerator] Initialized with Seed: {seed}");
        }

        // 3. Determine Networking Roles
        bool isNetworkedSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool isServer = isNetworkedSession && NetworkManager.Singleton.IsServer;

        // 4. Calculate Count (With Safety Cap)
        float area = mapWidth * mapHeight;
        int numAssets = Mathf.RoundToInt(area * assetDensity);

        if (numAssets > maxObjectLimit)
        {
            Debug.LogWarning($"[MapGenerator] Calculated {numAssets} assets, which exceeds limit of {maxObjectLimit}. Clamping to limit.");
            numAssets = maxObjectLimit;
        }

        Debug.Log($"[MapGenerator] Spawning {numAssets} assets on a {mapWidth}x{mapHeight} map.");

        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;
        float totalWeight = assets.Sum(a => a.weight);

        // 5. Spawn Loop
        for (int i = 0; i < numAssets; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-halfWidth, halfWidth),
                0f, 
                Random.Range(-halfHeight, halfHeight)
            );

            AssetData chosen = GetRandomAsset(totalWeight);
            if (chosen == null || chosen.assetPrefab == null) continue;

            // Logic:
            // - If Networked: Only SERVER spawns it (Netcode propagates to clients).
            // - If Local (Visuals): EVERYONE spawns it locally (Seed ensures same position).
            
            if (chosen.isNetworked)
            {
                if (isServer || !isNetworkedSession) // Only Server or Singleplayer
                {
                    GameObject obj = Instantiate(chosen.assetPrefab, pos, Quaternion.identity, transform);
                    var netObj = obj.GetComponent<NetworkObject>();
                    if (netObj != null && isNetworkedSession)
                    {
                        netObj.Spawn(true);
                    }
                }
            }
            else
            {
                // Local cosmetic (Grass, small rocks) - Spawns on all clients
                Instantiate(chosen.assetPrefab, pos, Quaternion.identity, transform);
            }
        }

        hasGenerated = true;
        Debug.Log("[MapGenerator] Generation Complete.");
    }

    private AssetData GetRandomAsset(float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var asset in assets)
        {
            cumulative += asset.weight;
            if (roll <= cumulative) return asset;
        }
        return assets[0];
    }

    public void ClearMap()
    {
        hasGenerated = false;
        
        // Destruir filhos (Assets Locais)
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // Destruir Objetos de Rede (Assets Networked)
        // Nota: Objetos spawnados via Netcode geralmente não ficam filhos do MapGenerator a menos que programado
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // Opcional: Se os teus networked objects tiverem uma tag ou script específico, destrói aqui.
            // O GameManager já limpa a maioria das coisas no Reset.
        }
    }
}