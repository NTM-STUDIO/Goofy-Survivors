using UnityEngine;
using System.Linq;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class MapConsumable : NetworkBehaviour
{
    [System.Serializable]
    public class DropItem
    {
        public GameObject itemPrefab;
        public float weight = 1f;
    }

    public DropItem[] possibleDrops;
    public ChestScript chestScript;
    private bool consumed = false;

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

        // If networking is active, let the server handle the interaction via RPC
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            var playerNO = other.GetComponentInParent<NetworkObject>();
            if (playerNO == null)
            {
                Debug.LogWarning("MapConsumable: Player NetworkObject not found in collider hierarchy; ignoring trigger.", other);
                return;
            }

            // Client asks server to process this interaction via the player's NetworkBehaviour (more reliable than from this object)
            if (!NetworkManager.Singleton.IsServer)
            {
                var pwm = playerNO.GetComponent<PlayerWeaponManager>();
                if (pwm != null)
                {
                    var thisNO = GetComponent<NetworkObject>();
                    if (thisNO != null && thisNO.IsSpawned)
                    {
                        pwm.RequestInteractWithConsumableServerRpc(thisNO.NetworkObjectId);
                    }
                }
                return; // don't process locally
            }

            // If we are the server (host), process immediately
            ServerProcessInteraction(playerNO.NetworkObjectId);
            return;
        }

        // Single-player fallback
        if (CompareTag("Chest") && chestScript != null)
        {
            var pwmLocal = other.GetComponentInParent<PlayerWeaponManager>();
            if (pwmLocal == null)
            {
                UnityEngine.Debug.LogWarning("MapConsumable (Chest SP): PlayerWeaponManager not found in collider hierarchy; ignoring trigger.");
                return;
            }
            chestScript.OpenChest(pwmLocal);
        }
        else
        {
            DropRandomItem();
        }

        Destroy(gameObject);
    }

    public void ServerProcessInteraction(ulong playerNetId)
    {
        if (consumed) return;

        if (CompareTag("Chest") && chestScript != null)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var playerNO) || playerNO == null)
            {
                Debug.LogWarning("MapConsumable Server: Player not found by NetId; aborting chest interaction.");
                return;
            }
            var pwm = playerNO.GetComponent<PlayerWeaponManager>();
            if (pwm == null)
            {
                Debug.LogWarning("MapConsumable Server: PlayerWeaponManager missing on player; aborting chest interaction.");
                return;
            }
            chestScript.OpenChest(pwm);
        }
        else
        {
            DropRandomItem();
        }

        consumed = true;
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(gameObject);
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
