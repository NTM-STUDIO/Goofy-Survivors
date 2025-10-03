using UnityEngine;

public class MapGenerator : MonoBehaviour
{


    [System.Serializable]
    public class TileData
    {
        public Sprite tileSprite;
        public float weight; // Quanto maior, mais comum é a tile
    }

    public TileData[] tiles;
    public int mapWidth = 4000;
    public int mapHeight = 2400;
    public GameObject tilePrefab; // Um GameObject com SpriteRenderer

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateMap();
    }

    void GenerateMap()
    {
        Debug.Log("Iniciando geração do mapa isométrico...");
        float totalWeight = 0f;
        foreach (var tile in tiles)
            totalWeight += tile.weight;

        var spriteRenderer = tilePrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("tilePrefab não tem SpriteRenderer!");
            return;
        }
        if (spriteRenderer.sprite == null)
        {
            Debug.LogError("tilePrefab não tem sprite atribuído!");
            return;
        }
        float tileWidth = spriteRenderer.sprite.bounds.size.x;
        float tileHeight = spriteRenderer.sprite.bounds.size.y;
        Debug.Log($"tileWidth: {tileWidth}, tileHeight: {tileHeight}");

        // Offset para alinhar o canto inferior esquerdo na origem
        float offsetX = (mapHeight - 1) * (tileWidth / 2f);
        float offsetY = 0f;

        int logCount = 0;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                TileData chosenTile = GetRandomTile(totalWeight);
                // Calcula a posição isométrica achatada
                float isoX = (x - y) * (tileWidth / 2f) + offsetX;
                float isoY = (y + x) * (tileHeight / 4f) + offsetY;
                Vector2 isoPosition = new Vector2(isoX, isoY);
                if (logCount < 10)
                {
                    Debug.Log($"Tile ({x},{y}) -> IsoPos: {isoPosition}");
                    logCount++;
                }
                GameObject tileGO = Instantiate(tilePrefab, isoPosition, Quaternion.identity, transform);
                tileGO.GetComponent<SpriteRenderer>().sprite = chosenTile.tileSprite;
                tileGO.GetComponent<SpriteRenderer>().sortingOrder = (mapWidth - x) + (mapHeight - y);
            }
        }
        Debug.Log("Geração do mapa isométrico finalizada.");
    }

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

        //erro
        Debug.LogWarning("Erro tile usada — verifica os pesos!");
        return tiles[tiles.Length - 1]; 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}