using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Collider))]
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
        // If this object is a chest, cache its ChestScript.
        if (CompareTag("Chest"))
        {
            chestScript = GetComponent<ChestScript>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // If it’s a chest, open it — otherwise drop a consumable.
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

    public void DropRandomItem()
    {
        if (possibleDrops == null || possibleDrops.Length == 0)
            return;

        float totalWeight = possibleDrops.Sum(d => d.weight);
        float random = Random.Range(0f, totalWeight);

        foreach (var drop in possibleDrops)
        {
            if (random <= drop.weight)
            {
                if (drop.itemPrefab != null)
                {
                    Instantiate(drop.itemPrefab, transform.position, Quaternion.identity);
                }
                return;
            }
            random -= drop.weight;
        }
    }
}
