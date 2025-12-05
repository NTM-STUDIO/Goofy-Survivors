using UnityEngine;
using Unity.Netcode;

// Attach this to a persistent server object (e.g., same GameObject as NetworkManager or a separate empty GO).
// Assign a player prefab with a NetworkObject component. Leave NetworkManager's PlayerPrefab empty.
public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;

    // Deprecated: Player spawning is now handled exclusively by GameManager.StartGame_P2P_Host().
    public override void OnNetworkSpawn()
    {
        // Intentionally left blank to avoid duplicate spawns.
        // Keep component in scene for now (safe to remove later).
    }
}
