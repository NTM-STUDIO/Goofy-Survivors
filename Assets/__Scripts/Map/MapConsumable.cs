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
        if (CompareTag("Chest"))
        {
            chestScript = GetComponent<ChestScript>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
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