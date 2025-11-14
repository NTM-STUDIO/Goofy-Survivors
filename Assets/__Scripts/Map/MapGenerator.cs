// Filename: MapGenerator.cs
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

    /// <summary>
    /// This is the single entry point for all map generation.
    /// The GameManager calls this on everyone via an RPC.
    /// </summary>
    public void GenerateMap()
    {
        if (hasGenerated)
        {
            Debug.Log("[MapGenerator] GenerateMap called, but map has already been generated. Skipping.");
            return;
        }

        Debug.Log($"[MapGenerator] Starting map generation. Is this the Server? -> {NetworkManager.Singleton.IsServer}");

        // 1. Calculate the total weight of ALL assets to create a single, unified probability pool.
        float totalAssetWeight = assets.Sum(asset => asset.weight);

        if (totalAssetWeight <= 0)
        {
            Debug.LogWarning("[MapGenerator] Total weight of all assets is zero. Nothing to spawn.");
            hasGenerated = true;
            return;
        }

        int numAssets = Mathf.RoundToInt(mapWidth * mapHeight * assetDensity);
        Debug.Log($"[MapGenerator] Spawning approximately {numAssets} total assets.");

        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;

        for (int i = 0; i < numAssets; i++)
        {
            Vector3 assetPosition = new Vector3(Random.Range(-halfWidth, halfWidth), 0f, Random.Range(-halfHeight, halfHeight));

            // 2. Choose an asset from the ENTIRE pool, respecting global weights.
            AssetData chosenAsset = GetRandomAsset(totalAssetWeight);
            if (chosenAsset == null || chosenAsset.assetPrefab == null) continue;

            // 3. Decide HOW to spawn based on the asset's flag.
            if (chosenAsset.isNetworked)
            {
                // If the chosen asset is networked, ONLY THE SERVER is allowed to spawn it.
                if (NetworkManager.Singleton.IsServer)
                {
                    GameObject instance = Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
                    var netObj = instance.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.Spawn(true);
                    }
                    else
                    {
                        Debug.LogError($"Asset '{instance.name}' is marked as Networked but is missing a NetworkObject component!", instance);
                    }
                }
            }
            else
            {
                // If the chosen asset is NOT networked, it's a local cosmetic.
                // EVERYONE (server and clients) spawns their own local copy.
                Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
            }
        }
        hasGenerated = true;
    }

    /// <summary>
    /// Selects a random asset from the main list based on the total weight provided.
    /// </summary>
    private AssetData GetRandomAsset(float totalWeight)
    {
        float randomValue = Random.Range(0, totalWeight);
        float cumulative = 0f;

        foreach (var asset in assets)
        {
            cumulative += asset.weight;
            if (randomValue <= cumulative)
                return asset;
        }
        // Fallback in case of floating point inaccuracies
        return assets.Length > 0 ? assets[assets.Length - 1] : null;
    }

    /// <summary>
    /// Resets the generator for a new game by destroying all spawned assets and resetting the flag.
    /// </summary>
    public void ResetGenerator()
    {
        hasGenerated = false;
        
        // Iterate through all child objects of this MapGenerator
        foreach (Transform child in transform)
        {
            var netObj = child.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // If it's a networked object, only the server can (and should) despawn it.
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && netObj.IsSpawned)
                {
                    netObj.Despawn(true); // true = destroy the object on all clients
                }
            }
            else
            {
                // If it's a non-networked (local) object, anyone can destroy their own copy.
                Destroy(child.gameObject);
            }
        }
        Debug.Log("[MapGenerator] Reset: cleared generated children and reset flag.");
    }
}