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
        // Inicia o processo de geração do mapa isométrico
        Debug.Log("Iniciando geração do mapa isométrico...");

        // Soma os pesos de todas as tiles para cálculo de probabilidade
        float totalWeight = 0f;
        foreach (var tile in tiles)
            totalWeight += tile.weight;

        // Valida o SpriteRenderer do prefab
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
        // Obtém o tamanho do tile em unidades do mundo
        float tileWidth = spriteRenderer.sprite.bounds.size.x;
        float tileHeight = spriteRenderer.sprite.bounds.size.y;
        Debug.Log($"tileWidth: {tileWidth}, tileHeight: {tileHeight}");

        // Calcula os limites para desenhar o mapa centrado em (0,0)
        int halfWidth = mapWidth / 2;
        int halfHeight = mapHeight / 2;

        int logCount = 0;
        for (int gridX = -halfWidth; gridX < halfWidth; gridX++) // Percorre colunas centradas
        {
            for (int gridY = -halfHeight; gridY < halfHeight; gridY++) // Percorre linhas centradas
            {
                // Escolhe uma tile aleatória com base nos pesos
                TileData chosenTile = GetRandomTile(totalWeight);

                // Converte as coordenadas do grid para coordenadas isométricas no mundo - mundo achatado
                float isoX = (gridX - gridY) * (tileWidth / 2f);
                float isoY = (gridY + gridX) * (tileHeight / 4f);
                Vector2 isoPosition = new Vector2(isoX, isoY);

                // Log das primeiras 5 tiles para debug
                if (logCount < 5)
                {
                    Debug.Log($"Tile ({gridX},{gridY}) -> IsoPos: {isoPosition}");
                    logCount++;
                }

                // Instancia o tile na posição calculada
                GameObject tileGO = Instantiate(tilePrefab, isoPosition, Quaternion.identity, transform);
                // Define o sprite da tile escolhida
                tileGO.GetComponent<SpriteRenderer>().sprite = chosenTile.tileSprite;
                // Define a ordem de renderização para garantir sobreposição correta
                tileGO.GetComponent<SpriteRenderer>().sortingOrder = (halfWidth * 2 - (gridX + halfWidth)) + (halfHeight * 2 - (gridY + halfHeight));
            }
        }
        // Fim da geração do mapa
        Debug.Log("Geração do mapa isométrico finalizada.");
    }

    // Função para escolher uma tile aleatória com base nos pesos
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

        // Caso algo corra mal, retorna a última tile e avisa no log
        Debug.LogWarning("Erro tile usada — verifica os pesos!");
        return tiles[tiles.Length - 1]; 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}