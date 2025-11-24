using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class ReviveManager : NetworkBehaviour
{
    [Header("Revive Settings")]
    [SerializeField] private float reviveRadius = 2.5f;
    [SerializeField] private float reviveTime = 5.0f;

    [Header("VFX")]
    [SerializeField] private Sprite reviveSprite;
    [SerializeField] private float reviveVfxYOffset = 2.0f;
    [SerializeField] private string reviveVfxSortingLayer = "VFX";
    [SerializeField] private float reviveVfxDuration = 0.8f;
    [SerializeField] private Color reviveColor = Color.white;
    
    private Dictionary<ulong, bool> playerAliveState = new Dictionary<ulong, bool>();
    private Dictionary<ulong, float> reviveProgress = new Dictionary<ulong, float>();

    public void RegisterPlayer(ulong clientId)
    {
        if (!playerAliveState.ContainsKey(clientId))
            playerAliveState[clientId] = true;
    }

    public void ResetReviveState()
    {
        playerAliveState.Clear();
        reviveProgress.Clear();
    }

    public void NotifyPlayerDowned(ulong clientId)
    {
        if (!GameManager.Instance.isP2P)
        {
            GameManager.Instance.GameOver();
            return;
        }

        if (IsServer)
        {
            // CORREÇÃO AQUI: Mudámos 'c' para 'client' para não dar conflito de nomes
            foreach(var client in NetworkManager.Singleton.ConnectedClientsList) 
                if(!playerAliveState.ContainsKey(client.ClientId)) playerAliveState[client.ClientId] = true;

            playerAliveState[clientId] = false;
            reviveProgress[clientId] = 0;
            
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var c) && c.PlayerObject)
                SetDownedVisualClientRpc(c.PlayerObject.NetworkObjectId, true);

            CheckGameOverCondition();
        }
        else
        {
            NotifyDownedServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyDownedServerRpc(ulong clientId) => NotifyPlayerDowned(clientId);

    public void ServerApplyPlayerDamage(ulong targetId, float amount, Vector3? pos, float? iframe)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out var c) && c.PlayerObject)
        {
            var stats = c.PlayerObject.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.ApplyDamage(amount, pos, iframe);
                SyncHpClientRpc(c.PlayerObject.NetworkObjectId, stats.CurrentHp, stats.maxHp);
            }
        }
    }

    [ClientRpc]
    private void SyncHpClientRpc(ulong netId, int hp, int max)
    {
        if(NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var o))
            o.GetComponent<PlayerStats>()?.ClientSyncHp(hp, max);
    }

    private void Update()
    {
        if (!IsServer || !GameManager.Instance.isP2P || GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        var downedPlayers = playerAliveState.Where(x => !x.Value).Select(x => x.Key).ToList();

        foreach (var downedId in downedPlayers)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(downedId, out var downedClient)) continue;
            if (downedClient.PlayerObject == null) continue;
            
            bool rescuerNear = false;
            foreach (var rescuer in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (rescuer.ClientId == downedId) continue;
                if (!playerAliveState.GetValueOrDefault(rescuer.ClientId, false)) continue;
                if (rescuer.PlayerObject == null) continue;
                
                if (Vector3.Distance(downedClient.PlayerObject.transform.position, rescuer.PlayerObject.transform.position) <= reviveRadius)
                {
                    rescuerNear = true;
                    break;
                }
            }

            if (rescuerNear)
            {
                reviveProgress[downedId] = reviveProgress.GetValueOrDefault(downedId, 0) + Time.deltaTime;
                if (reviveProgress[downedId] >= reviveTime)
                {
                    // Revive!
                    playerAliveState[downedId] = true;
                    reviveProgress.Remove(downedId);
                    
                    var ps = downedClient.PlayerObject.GetComponent<PlayerStats>();
                    ps?.ServerReviveToFixedHp(10);
                    
                    SetDownedVisualClientRpc(downedClient.PlayerObject.NetworkObjectId, false);
                    PlayReviveVFXClientRpc(downedClient.PlayerObject.NetworkObjectId);
                    SyncHpClientRpc(downedClient.PlayerObject.NetworkObjectId, 10, ps.maxHp);
                }
            }
            else
            {
                reviveProgress[downedId] = 0;
            }
        }
        
        CheckGameOverCondition();
    }

    private void CheckGameOverCondition()
    {
        if (playerAliveState.Count > 0 && !playerAliveState.Values.Any(alive => alive))
        {
            GameManager.Instance.GameOver();
        }
    }

    [ClientRpc]
    private void SetDownedVisualClientRpc(ulong netId, bool downed)
    {
        if(NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var obj))
        {
            var stats = obj.GetComponent<PlayerStats>();
            if (stats == null) return;

            var sr = obj.GetComponentInChildren<SpriteRenderer>();
            
            if (downed)
            {
                stats.ClientApplyDownedState();
                if (sr && stats.DownedSprite) sr.sprite = stats.DownedSprite;
                if (sr) sr.sortingLayerName = "MAPCOSMETIC";
            }
            else
            {
                stats.ClientApplyRevivedState();
                if (sr && stats.OriginalSprite) sr.sprite = stats.OriginalSprite;
                if (sr && !string.IsNullOrEmpty(stats.OriginalSortingLayer)) sr.sortingLayerName = stats.OriginalSortingLayer;
            }
        }
    }

    [ClientRpc]
    private void PlayReviveVFXClientRpc(ulong netId)
    {
        if(NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var obj))
        {
            // Assume que tens uma classe estática ReviveVFX ou similar, ou remove se não tiveres
             ReviveVFX.Spawn(obj.transform.position, reviveSprite, reviveColor, reviveVfxDuration, 0.8f, 1.3f, reviveVfxYOffset, reviveVfxSortingLayer);
        }
    }
}