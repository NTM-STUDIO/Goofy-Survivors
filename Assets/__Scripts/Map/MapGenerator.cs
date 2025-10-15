using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [System.Serializable]
    public class TileData
    {
        public Sprite tileSprite;
        public float weight; // Quanto maior, mais comum é a tile
    }

    [System.Serializable]
    public class AssetData
    {
        public GameObject assetPrefab;
        public float weight; // Quanto maior, mais comum é o asset
    }

    public TileData[] tiles;
    public AssetData[] assets; // Lista de assets para spawnar
    public int mapWidth = 2000;
    public int mapHeight = 1200;
    public GameObject tilePrefab; // Um GameObject com SpriteRenderer

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateMap();
    }

    void GenerateMap()
    {


        // Soma os pesos de todas as tiles para c�lculo de probabilidade
        float totalWeight = 0f;
        foreach (var tile in tiles)
            totalWeight += tile.weight;

        // Soma os pesos dos assets
        float totalAssetWeight = 0f;
        foreach (var asset in assets)
            totalAssetWeight += asset.weight;


        // Obtem o tamanho do tile em unidades do mundo
        float tileWidth = 7.3f;
        float tileHeight = 4.44f;

        // Calcula os limites para desenhar o mapa centrado em (0,0)
        int halfWidth = mapWidth / 2;
        int halfHeight = mapHeight / 2;

        int logCount = 0;
        for (int gridX = -halfWidth; gridX < halfWidth; gridX++) // Percorre colunas centradas
        {
            for (int gridY = -halfHeight; gridY < halfHeight; gridY++) // Percorre linhas centradas
            {
                // Escolhe uma tile aleat�ria com base nos pesos
                TileData chosenTile = GetRandomTile(totalWeight);

                // Converte as coordenadas do grid para coordenadas isom�tricas no mundo - mundo achatado
                float isoX = (gridX - gridY) * (tileWidth) / 2.06f;
                float isoY = (gridY + gridX) * (tileHeight) / 2.06f;
                Vector2 isoPosition = new Vector2(isoX, isoY);

                // Log das primeiras 5 tiles para debug
                if (logCount < 2)
                {
                    logCount++;
                }

                // Instancia o tile na posi��o calculada
                GameObject tileGO = Instantiate(tilePrefab, isoPosition, Quaternion.identity, transform);
                // Define o sprite da tile escolhida
                tileGO.GetComponent<SpriteRenderer>().sprite = chosenTile.tileSprite;
                // Define a ordem de renderiza��o para garantir sobreposi��o correta
                tileGO.GetComponent<SpriteRenderer>().sortingOrder = -(gridX + gridY);
                //(halfWidth * 2 - (gridX + halfWidth)) + (halfHeight * 2 - (gridY + halfHeight));

                // Spawn de asset aleatório sobre a tile
                if (assets != null && assets.Length > 0)
                {
                    AssetData chosenAsset = GetRandomAsset(totalAssetWeight);
                    if (chosenAsset != null && chosenAsset.assetPrefab != null)
                    {
                        Vector3 assetPosition = new Vector3(isoPosition.x, isoPosition.y, -1f); // Z para ficar acima da tile
                        Instantiate(chosenAsset.assetPrefab, assetPosition, Quaternion.identity, transform);
                    }
                }
            }
        }
    }

    // Fun��o para escolher uma tile aleat�ria com base nos pesos
    TileData GetRandomTile(float totalWeight)
    {
        float randomValue = Random.Range(0, totalWeight);
        float cumulative = 0f;

        foreach (var tile in tiles)
        {
            cumulative += tile.weight;
            if (randomValue <= cumulative)
                return tile;
        }

        // Caso algo corra mal, retorna a �ltima tile e avisa no log
        Debug.LogWarning("Erro tile usada � verifica os pesos!");
        return tiles[tiles.Length - 1];
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