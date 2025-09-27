using UnityEngine;

public class MapGenerator : MonoBehaviour
{


    [System.Serializable]
    public class TileData
    {
        public Sprite tileSprite;
        public float weight; // Quanto maior, mais comum
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
        //calcula as probabilidades das tiles
        float totalWeight = 0f;
        foreach (var tile in tiles)
            totalWeight += tile.weight;
        //percorre cada posição da matriz e escolhe aleatoriamente uma tile
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                TileData chosenTile = GetRandomTile(totalWeight);
                Vector2 position = new Vector2(x, y);
                GameObject tileGO = Instantiate(tilePrefab, position, Quaternion.identity, transform);
                tileGO.GetComponent<SpriteRenderer>().sprite = chosenTile.tileSprite;
            }
        }
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