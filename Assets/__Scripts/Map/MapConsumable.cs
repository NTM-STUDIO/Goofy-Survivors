using UnityEngine;
using System.Linq;

public class MapConsumable : MonoBehaviour
{
    [System.Serializable]
    public class DropItem
    {
        public GameObject itemPrefab;
        public float weight = 1f;
    }

    public DropItem[] possibleDrops;
    public ChestScript chestScript;

    private void Awake()
    {
        // Esta lógica permanece a mesma.
        if (CompareTag("Chest"))
        {
            chestScript = GetComponent<ChestScript>();
        }
    }

    // --- FUNÇÃO MODIFICADA PARA 3D ---
    // Trocámos OnTriggerEnter2D por OnTriggerEnter e Collider2D por Collider.
    private void OnTriggerEnter(Collider other)
    {
        // O resto da lógica dentro da função é exatamente o mesmo,
        // pois CompareTag funciona para colisores 2D e 3D.
        if (other.CompareTag("Player"))
        {
            if (CompareTag("Chest") && chestScript != null)
            {
                chestScript.OpenChest();
            }
            else
            {
                DropRandomItem();
            }
            
            Destroy(gameObject);
        }
    }

    public void DropRandomItem()
    {
        // Esta lógica permanece a mesma.
        if (possibleDrops.Length == 0) return;

        float totalWeight = possibleDrops.Sum(drop => drop.weight);
        float randomNumber = Random.Range(0f, totalWeight);

        foreach (var drop in possibleDrops)
        {
            if (randomNumber <= drop.weight)
            {
                Instantiate(drop.itemPrefab, transform.position, Quaternion.identity);
                return;
            }
            randomNumber -= drop.weight;
        }
    }
}