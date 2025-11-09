using UnityEngine;
using Unity.Netcode;

// Attach this to a persistent server object (e.g., same GameObject as NetworkManager or a separate empty GO).
// Assign a player prefab with a NetworkObject component. Leave NetworkManager's PlayerPrefab empty.
public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer || playerPrefab == null) return;

        // Simple spawn position spacing by clientId
        var spawnPos = new Vector3((int)clientId * 2f, 0f, 0f);
        var go = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("Player prefab must have a NetworkObject component.");
            Destroy(go);
            return;
        }
        // Spawn as the player's owned object so you can use IsOwner logic
        netObj.SpawnAsPlayerObject(clientId);
    }
}
