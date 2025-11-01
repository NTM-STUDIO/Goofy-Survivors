using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Spawns shared map assets. In multiplayer, only the server/host runs this so assets are synchronized.
/// In single-player, it just instantiates locally.
/// </summary>
public class MapAssetSpawner : MonoBehaviour
{
    [Header("Spawn Mode")]
    [Tooltip("If true, use explicit spawn points; otherwise, randomize within the defined area.")]
    public bool useExplicitPoints = true;

    [Header("Assets to Spawn")]
    [Tooltip("Prefabs to spawn. Prefabs intended for MP should have NetworkObject and be added to NetworkPrefabs.")]
    public List<GameObject> assetPrefabs = new List<GameObject>();

    [Header("Explicit Spawn Points")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Random Area Settings")]
    public Vector3 areaCenter = Vector3.zero;
    public Vector3 areaSize = new Vector3(50, 0, 50);
    public int randomCountPerPrefab = 3;

    [Header("Y Placement")] 
    [Tooltip("Optional fixed Y for spawned assets; set to NaN to preserve prefab's Y.")]
    public float fixedY = float.NaN;

    private GameManager gm;

    void Awake()
    {
        gm = GameManager.Instance;
    }

    public void SpawnAssets()
    {
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && gm != null && gm.isP2P;
        bool shouldRun = !isNetworked || (isNetworked && NetworkManager.Singleton.IsServer);
        if (!shouldRun) return; // Only host/server executes in MP

        if (assetPrefabs == null || assetPrefabs.Count == 0) return;

        if (useExplicitPoints)
        {
            for (int i = 0; i < assetPrefabs.Count; i++)
            {
                var prefab = assetPrefabs[i];
                if (prefab == null) continue;
                // If there are fewer points than prefabs, loop
                if (spawnPoints == null || spawnPoints.Count == 0)
                {
                    Debug.LogWarning("MapAssetSpawner: No spawn points configured.");
                    break;
                }
                for (int p = 0; p < spawnPoints.Count; p++)
                {
                    Vector3 pos = spawnPoints[p] != null ? spawnPoints[p].position : areaCenter;
                    if (!float.IsNaN(fixedY)) pos.y = fixedY;
                    SpawnOne(prefab, pos, spawnPoints[p] != null ? spawnPoints[p].rotation : Quaternion.identity, isNetworked);
                }
                // Only spawn once per point-set if you intend 1:1; comment out break to spawn all prefabs at all points
                break;
            }
        }
        else
        {
            foreach (var prefab in assetPrefabs)
            {
                if (prefab == null) continue;
                int count = Mathf.Max(1, randomCountPerPrefab);
                for (int i = 0; i < count; i++)
                {
                    Vector3 random = new Vector3(
                        Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                        0f,
                        Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f)
                    );
                    Vector3 pos = areaCenter + random;
                    if (!float.IsNaN(fixedY)) pos.y = fixedY;
                    SpawnOne(prefab, pos, Quaternion.identity, isNetworked);
                }
            }
        }
    }

    private void SpawnOne(GameObject prefab, Vector3 position, Quaternion rotation, bool isNetworked)
    {
        var go = Instantiate(prefab, position, rotation);
        if (isNetworked)
        {
            var net = go.GetComponent<NetworkObject>();
            if (net != null)
            {
                net.Spawn(true);
            }
            else
            {
                Debug.LogWarning($"MapAssetSpawner: Prefab '{prefab.name}' has no NetworkObject; it won't sync in MP.");
            }
        }
    }
}
