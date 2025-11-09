using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class MapGenerator : MonoBehaviour
{
    [System.Serializable]
    public class AssetData
    {
        public GameObject assetPrefab;
        public float weight; // Quanto maior, mais comum é o asset
    }

    [Header("Generation Settings")]
    public AssetData[] assets;
    public int mapWidth = 2000;
    public int mapHeight = 1200;
    public float assetDensity = 0.1f; // Adjust for more or fewer assets
    private bool hasGenerated = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Do not generate on Start. Generation is explicitly triggered by GameManager
        // when the player clicks Play (SP) or Host (MP).
        Debug.Log("[MapGenerator] Start() - waiting for GameManager to trigger generation.");
    }

    public void GenerateLocal()
    {
        if (hasGenerated)
        {
            Debug.Log("[MapGenerator] GenerateMapLocal skipped (already generated).");
            return;
        }
        // Soma os pesos dos assets
        float totalAssetWeight = 0f;
        foreach (var asset in assets)
            totalAssetWeight += asset.weight;

        // Calculate number of assets based on density and map size
        int numAssets = Mathf.RoundToInt(mapWidth * mapHeight * assetDensity);
        Debug.Log($"Spawning {numAssets} assets.");

        // Calculate boundaries to keep assets centered at (0, 0)
        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;

    for (int i = 0; i < numAssets; i++)
        {
            // Generate random position within boundaries
            float x = Random.Range(-halfWidth, halfWidth);
            float z = Random.Range(-halfHeight, halfHeight);
            Vector3 assetPosition = new Vector3(x, 0f, z); // Y = 0 for flat ground

            // Get random asset
            AssetData chosenAsset = GetRandomAsset(totalAssetWeight);

            // If asset is valid, instantiate
            if (chosenAsset != null && chosenAsset.assetPrefab != null)
            {
                Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
            }
        }
        hasGenerated = true;
    }

    // Called by GameManager host to generate assets visible to all players
    public void GenerateNetworked()
    {
        // Only host/server should generate and spawn networked objects
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (hasGenerated)
        {
            Debug.Log("[MapGenerator] GenerateNetworked skipped (already generated).");
            return;
        }

        float totalAssetWeight = 0f;
        foreach (var asset in assets)
            totalAssetWeight += asset.weight;

        int numAssets = Mathf.RoundToInt(mapWidth * mapHeight * assetDensity);
        Debug.Log($"[MapGenerator] Host spawning {numAssets} shared assets.");

        float halfWidth = mapWidth * 0.5f;
        float halfHeight = mapHeight * 0.5f;

        for (int i = 0; i < numAssets; i++)
        {
            float x = Random.Range(-halfWidth, halfWidth);
            float z = Random.Range(-halfHeight, halfHeight);
            Vector3 assetPosition = new Vector3(x, 0f, z);

            AssetData chosenAsset = GetRandomAsset(totalAssetWeight);
            if (chosenAsset == null || chosenAsset.assetPrefab == null) continue;

            // Ensure the prefab is registered with Netcode so clients can spawn it
            TryRegisterNetworkPrefab(chosenAsset.assetPrefab);

            GameObject go = Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
            // Remove any nested NetworkObjects under this spawned root to avoid Netcode nested spawn errors
            RemoveNestedChildNetworkObjects(go);
            // Delay one frame so Destroy(component) completes before spawning
            StartCoroutine(SpawnAfterStrip(go));
        }

        // Also ensure any pre-placed children of MapGenerator are network-spawned (e.g., Chest, Arbusto placed in scene)
        EnsureChildrenNetworkSpawned();
        hasGenerated = true;
    }

    // Clear all generated children and allow generation again (singleplayer)
    public void ResetLocal()
    {
        // Destroy all children spawned under this generator
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            children.Add(child);
        }
        foreach (var t in children)
        {
            if (t != null) Destroy(t.gameObject);
        }
        hasGenerated = false;
        Debug.Log("[MapGenerator] ResetLocal: cleared generated children and reset flag.");
    }

    private void TryRegisterNetworkPrefab(GameObject prefab)
    {
        RuntimeNetworkPrefabRegistry.TryRegister(prefab);
    }

    // Ensures all direct children under this generator are networked and spawned on clients
    private void EnsureChildrenNetworkSpawned()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        int count = 0;
    foreach (Transform child in transform)
        {
            // Skip if this object was already spawned
            // Clean nested NetworkObjects beneath this root to avoid nested spawn errors
            RemoveNestedChildNetworkObjects(child.gameObject);
            // Delay spawning to next frame to ensure components are destroyed
            StartCoroutine(SpawnAfterStrip(child.gameObject, () => { count++; }));
        }
        if (count > 0)
        {
            Debug.Log($"[MapGenerator] Ensured {count} pre-placed children are network-spawned.");
        }
    }

    AssetData GetRandomAsset(float totalWeight)
    {
        float randomValue = Random.Range(0, totalWeight);
        float cumulative = 0f;

        foreach (var asset in assets)
        {
            cumulative += asset.weight;
            if (randomValue <= cumulative)
                return asset;
        }
        return assets[assets.Length - 1];
    }

    // Detach any child NetworkObjects to avoid nested NOs; reparent them under the MapGenerator so they can be spawned separately
    private void RemoveNestedChildNetworkObjects(GameObject root)
    {
        var allNOs = root.GetComponentsInChildren<NetworkObject>(true);
        foreach (var no in allNOs)
        {
            if (no == null) continue;
            if (no.gameObject == root) continue; // keep root's NO intact

            Debug.LogWarning($"[MapGenerator] Detaching nested NetworkObject child '{no.gameObject.name}' from '{root.name}' to avoid nested NO hierarchy.");

            // Reparent to the MapGenerator so it becomes a sibling rather than a child
            no.transform.SetParent(this.transform, true);

            // Important: DO NOT destroy the NetworkObject here; other components may require it (e.g., GuaranteedRarityGiver)
            // We'll spawn all direct children later via EnsureChildrenNetworkSpawned()
        }
    }

    // Spawns the root as a network object after a frame so that any destroyed child NetworkObjects are fully removed
    private IEnumerator SpawnAfterStrip(GameObject root, System.Action onSpawned = null)
    {
        // Only server should proceed
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) yield break;

        // Wait one frame to let Destroy(component) finalize
        yield return null;

        if (root == null) yield break;

        var netObj = root.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            netObj = root.AddComponent<NetworkObject>();
        }

        if (!netObj.IsSpawned)
        {
            netObj.Spawn(true);
            Debug.Log($"[MapGenerator] Spawned '{root.name}' with NetId={netObj.NetworkObjectId} at {root.transform.position}");
            onSpawned?.Invoke();
        }
    }
}