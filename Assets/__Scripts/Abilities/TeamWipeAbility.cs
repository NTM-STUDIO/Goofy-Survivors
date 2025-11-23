using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TeamWipeAbility : NetworkBehaviour
{
    public float activationDistance = 3.0f;
    public float enemyWipeRadius = 8.0f;
    public KeyCode activationKey = KeyCode.E;
    private static HashSet<ulong> playersReady = new HashSet<ulong>();
    private static float readyTimeout = 5f;
    private static Dictionary<ulong, float> readyTimestamps = new Dictionary<ulong, float>();

    void Update()
    {
        if (!IsOwner || !NetworkManager.Singleton.IsConnectedClient || !GameManager.Instance.isP2P)
            return;

        if (Input.GetKeyDown(activationKey))
        {
            TrySetReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    [ServerRpc]
    private void TrySetReadyServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
    {
        // Clean up expired ready states
        float now = Time.time;
        List<ulong> expired = new List<ulong>();
        foreach (var kv in readyTimestamps)
        {
            if (now - kv.Value > readyTimeout)
                expired.Add(kv.Key);
        }
        foreach (var id in expired)
        {
            playersReady.Remove(id);
            readyTimestamps.Remove(id);
        }

        // Mark this player as ready
        playersReady.Add(clientId);
        readyTimestamps[clientId] = now;

        // Check for another player in range who is also ready
        foreach (var otherClient in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong otherId = otherClient.ClientId;
            if (otherId == clientId || !playersReady.Contains(otherId))
                continue;

            var playerA = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            var playerB = NetworkManager.Singleton.ConnectedClients[otherId].PlayerObject;
            if (playerA == null || playerB == null)
                continue;

            float dist = Vector3.Distance(playerA.transform.position, playerB.transform.position);
            if (dist <= activationDistance)
            {
                // Both are close and ready: trigger ability
                playersReady.Remove(clientId);
                playersReady.Remove(otherId);
                readyTimestamps.Remove(clientId);
                readyTimestamps.Remove(otherId);
                WipeEnemiesNearPlayers(playerA.transform.position, playerB.transform.position);
                break;
            }
        }
    }

    private void WipeEnemiesNearPlayers(Vector3 posA, Vector3 posB)
    {
        // Find all enemies within radius of either player
        var allEnemies = GameObject.FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;
            float distA = Vector3.Distance(enemy.transform.position, posA);
            float distB = Vector3.Distance(enemy.transform.position, posB);
            if (distA <= enemyWipeRadius || distB <= enemyWipeRadius)
            {
                // Networked destroy
                var netObj = enemy.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    GameObject.Destroy(enemy.gameObject);
                }
            }
        }
    }
}
