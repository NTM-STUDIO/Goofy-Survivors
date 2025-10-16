using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [System.Serializable]
    public class AssetData
    {
        public GameObject assetPrefab;
        public float weight; // Quanto maior, mais comum é o asset
    }

    public AssetData[] assets; // Lista de assets para spawnar
    public int mapWidth = 2000;
    public int mapHeight = 1200;
    public float assetDensity = 0.1f; // Adjust for more or fewer assets

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateMap();
    }

    void GenerateMap()
    {
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
                // Instantiating the Asset
                Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
            }
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
}