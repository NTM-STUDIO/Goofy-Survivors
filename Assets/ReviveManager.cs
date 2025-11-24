using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class ReviveManager : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float reviveRadius = 3.0f;
    [SerializeField] private float reviveTime = 5.0f;

    [Header("Visuals")]
    [SerializeField] private LineRenderer reviveBeam; // Arraste o LineRenderer aqui
    [SerializeField] private Sprite reviveSprite;
    [SerializeField] private string reviveVfxSortingLayer = "VFX";

    private Dictionary<ulong, bool> playerAliveState = new Dictionary<ulong, bool>();
    private Dictionary<ulong, float> reviveProgress = new Dictionary<ulong, float>();

    // Variável para sincronizar o feixe visual (Quem está a salvar -> Quem)
    private NetworkVariable<Vector2Int> activeReviveLink = new NetworkVariable<Vector2Int>(
        new Vector2Int(-1, -1), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (reviveBeam == null) reviveBeam = GetComponent<LineRenderer>();
    }

    public void RegisterPlayer(ulong clientId)
    {
        if (!playerAliveState.ContainsKey(clientId)) playerAliveState[clientId] = true;
    }

    public void ResetReviveState()
    {
        playerAliveState.Clear();
        reviveProgress.Clear();
        if(IsServer) activeReviveLink.Value = new Vector2Int(-1, -1);
    }

    public void NotifyPlayerDowned(ulong clientId)
    {
        if (!GameManager.Instance.isP2P) { GameManager.Instance.GameOver(); return; }

        if (IsServer)
        {
            // CORREÇÃO AQUI: Mudei 'c' para 'client'
            foreach(var client in NetworkManager.Singleton.ConnectedClientsList) 
                if(!playerAliveState.ContainsKey(client.ClientId)) playerAliveState[client.ClientId] = true;

            playerAliveState[clientId] = false;
            reviveProgress[clientId] = 0f;
            
            // CORREÇÃO AQUI: Mudei 'out var c' para 'out var downedClient'
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var downedClient) && downedClient.PlayerObject)
                SetDownedVisualClientRpc(downedClient.PlayerObject.NetworkObjectId, true);

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
        UpdateVisualBeam(); 

        if (!IsServer || !GameManager.Instance.isP2P || GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        var downedPlayers = playerAliveState.Where(x => !x.Value).Select(x => x.Key).ToList();
        bool anyoneReviving = false;

        foreach (var downedId in downedPlayers)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(downedId, out var downedClient)) continue;
            if (downedClient.PlayerObject == null) continue;
            
            bool rescuerNear = false;
            ulong currentRescuerID = 0;

            foreach (var rescuer in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (rescuer.ClientId == downedId) continue;
                if (!playerAliveState.GetValueOrDefault(rescuer.ClientId, true)) continue; 
                if (rescuer.PlayerObject == null) continue;
                
                if (Vector3.Distance(downedClient.PlayerObject.transform.position, rescuer.PlayerObject.transform.position) <= reviveRadius)
                {
                    rescuerNear = true;
                    currentRescuerID = rescuer.ClientId;
                    break;
                }
            }

            if (rescuerNear)
            {
                float currentProgress = reviveProgress.GetValueOrDefault(downedId, 0f);
                currentProgress += Time.deltaTime;
                reviveProgress[downedId] = currentProgress;

                activeReviveLink.Value = new Vector2Int((int)currentRescuerID, (int)downedId);
                anyoneReviving = true;

                if (currentProgress >= reviveTime)
                {
                    playerAliveState[downedId] = true;
                    reviveProgress.Remove(downedId);
                    var ps = downedClient.PlayerObject.GetComponent<PlayerStats>();
                    if (ps != null) {
                        ps.ServerReviveToFixedHp(10);
                        SyncHpClientRpc(downedClient.PlayerObject.NetworkObjectId, 10, ps.maxHp);
                    }
                    SetDownedVisualClientRpc(downedClient.PlayerObject.NetworkObjectId, false);
                    activeReviveLink.Value = new Vector2Int(-1, -1); 
                }
            }
            else
            {
                if (reviveProgress.ContainsKey(downedId)) reviveProgress[downedId] = 0f;
            }
        }

        if (!anyoneReviving && activeReviveLink.Value.x != -1)
        {
            activeReviveLink.Value = new Vector2Int(-1, -1);
        }
        
        CheckGameOverCondition();
    }

    private void UpdateVisualBeam()
    {
        if (reviveBeam == null) return;

        Vector2Int link = activeReviveLink.Value;
        
        if (link.x == -1 || link.y == -1)
        {
            reviveBeam.enabled = false;
            return;
        }

        var nm = NetworkManager.Singleton;
        if (nm != null 
            && nm.ConnectedClients.TryGetValue((ulong)link.x, out var rescuer) 
            && nm.ConnectedClients.TryGetValue((ulong)link.y, out var downed))
        {
            if (rescuer.PlayerObject != null && downed.PlayerObject != null)
            {
                reviveBeam.enabled = true;
                reviveBeam.SetPosition(0, rescuer.PlayerObject.transform.position + Vector3.up);
                reviveBeam.SetPosition(1, downed.PlayerObject.transform.position + Vector3.up);
                return;
            }
        }

        reviveBeam.enabled = false;
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
            var sr = obj.GetComponentInChildren<SpriteRenderer>();

            if (stats == null) return;

            if (downed)
            {
                stats.ClientApplyDownedState();
                if (sr) sr.color = Color.gray; 
            }
            else
            {
                stats.ClientApplyRevivedState();
                if (sr) sr.color = Color.white;
            }
        }
    }
}