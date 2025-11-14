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
        public float weight;
        [Tooltip("Check this if the asset should be synchronized for all players. Uncheck for local-only cosmetics.")]
        public bool isNetworked = true;
    }

    [Header("Generation Settings")]
    public AssetData[] assets;
    public int mapWidth = 2000;
    public int mapHeight = 1200;
    public float assetDensity = 0.1f;
    private bool hasGenerated = false;

    void Start()
    {
        Debug.Log("[MapGenerator] Start() - waiting for GameManager to trigger generation.");
    }

    public void GenerateMap()
    {
        if (hasGenerated)
        {
            Debug.Log("[MapGenerator] GenerateMap called, but map has already been generated. Skipping.");
            return;
        }

        // --- FIX: Determine if this is a multiplayer session or single-player ---
        bool isNetworkedSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        // The authority is the server in a multiplayer game, OR the player in a single-player game.
        bool isAuthority = isNetworkedSession ? NetworkManager.Singleton.IsServer : true;

        Debug.Log($"[MapGenerator] Starting map generation. Is Authority? -> {isAuthority} (Networked: {isNetworkedSession})");

        float totalAssetWeight = assets.Sum(asset => asset.weight);

        if (totalAssetWeight <= 0)
        {
            Debug.LogWarning("[MapGenerator] Total weight of all assets is zero. Nothing to spawn.");
            hasGenerated = true;
            return;
        }

        int numAssets = Mathf.RoundToInt((float)mapWidth * mapHeight * assetDensity);
        Debug.Log($"[MapGenerator] Spawning approximately {numAssets} total assets.");

        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;

        for (int i = 0; i < numAssets; i++)
        {
            Vector3 assetPosition = new Vector3(Random.Range(-halfWidth, halfWidth), 0f, Random.Range(-halfHeight, halfHeight));
            AssetData chosenAsset = GetRandomAsset(totalAssetWeight);
            if (chosenAsset == null || chosenAsset.assetPrefab == null) continue;

            if (chosenAsset.isNetworked)
            {
                // --- FIX: Only the authority can spawn networked/master objects ---
                if (isAuthority)
                {
                    GameObject instance = Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
                    var netObj = instance.GetComponent<NetworkObject>();

                    // If we are in a multiplayer game, we also need to spawn it over the network.
                    // In single-player, we just instantiate it.
                    if (isNetworkedSession && netObj != null)
                    {
                        netObj.Spawn(true);
                    }
                    else if (netObj == null)
                    {
                        Debug.LogError($"Asset '{instance.name}' is marked as Networked but is missing a NetworkObject component!", instance);
                    }
                }
            }
            else
            {
                // Local cosmetics are spawned by everyone (clients and server/single-player). This logic is correct.
                Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
            }
        }
        hasGenerated = true;
    }

    private AssetData GetRandomAsset(float totalWeight)
    {
        if (totalWeight <= 0) return null;
        float randomValue = Random.Range(0, totalWeight);
        float cumulative = 0f;

        foreach (var asset in assets)
        {
            cumulative += asset.weight;
            if (randomValue <= cumulative)
                return asset;
        }
        return assets.Length > 0 ? assets[assets.Length - 1] : null;
    }

    public void ResetGenerator()
    {
        hasGenerated = false;
        
        // --- FIX: Determine session type to correctly despawn/destroy objects ---
        bool isNetworkedSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool isServer = isNetworkedSession && NetworkManager.Singleton.IsServer;

        // Use a temporary list to avoid issues with modifying the collection while iterating
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            childrenToDestroy.Add(child.gameObject);
        }

        foreach (var childGO in childrenToDestroy)
        {
            var netObj = childGO.GetComponent<NetworkObject>();
            
            // If it's a spawned network object in a server- authoritative session...
            if (netObj != null && isServer && netObj.IsSpawned)
            {
                // Only the server can despawn it.
                netObj.Despawn(true);
            }
            else
            {
                // This will correctly destroy:
                // 1. All local (non-networked) objects on every client/server.
                // 2. All "networked" objects when playing in single-player mode.
                Destroy(childGO);
            }
        }
        Debug.Log("[MapGenerator] Reset: cleared generated children and reset flag.");
    }
}