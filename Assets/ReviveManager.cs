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
    [SerializeField] private LineRenderer reviveBeam; // ARRASTA O LINE RENDERER AQUI
    [SerializeField] private Sprite reviveSprite;
    [SerializeField] private Color reviveColor = Color.green;

    private Dictionary<ulong, bool> playerAliveState = new Dictionary<ulong, bool>();
    private Dictionary<ulong, float> reviveProgress = new Dictionary<ulong, float>();

    // Sincroniza visualmente quem está a salvar quem (X=Salvador, Y=Morto)
    private NetworkVariable<Vector2Int> activeReviveLink = new NetworkVariable<Vector2Int>(
        new Vector2Int(-1, -1), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (reviveBeam == null) reviveBeam = GetComponent<LineRenderer>();
        if (reviveBeam != null) reviveBeam.enabled = false;
    }

    public void RegisterPlayer(ulong clientId) 
    { 
        if (!playerAliveState.ContainsKey(clientId)) playerAliveState[clientId] = true; 
    }

    public void ResetReviveState() {
        playerAliveState.Clear(); 
        reviveProgress.Clear();
        if(IsServer) activeReviveLink.Value = new Vector2Int(-1, -1);
    }

    public void NotifyPlayerDowned(ulong clientId) {
        if (!GameManager.Instance.isP2P) { GameManager.Instance.GameOver(); return; }
        
        if (IsServer) {
            // CORREÇÃO: Mudei 'c' para 'client' para evitar conflito
            foreach(var client in NetworkManager.Singleton.ConnectedClientsList) 
            {
                if(!playerAliveState.ContainsKey(client.ClientId)) 
                    playerAliveState[client.ClientId] = true;
            }

            playerAliveState[clientId] = false; 
            reviveProgress[clientId] = 0f;
            
            // CORREÇÃO: Mudei 'out var c' para 'out var downedClient'
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var downedClient) && downedClient.PlayerObject != null) 
            {
                downedClient.PlayerObject.GetComponent<PlayerStats>()?.SetDownedState(true);
            }

            CheckGameOverCondition();
        } 
        else 
        {
            NotifyDownedServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)] 
    private void NotifyDownedServerRpc(ulong clientId) => NotifyPlayerDowned(clientId);

    public void ServerApplyPlayerDamage(ulong id, float a, Vector3? p, float? i) {
        if (!IsServer) return;
        // CORREÇÃO: Mudei nome para 'targetClient' para clareza
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(id, out var targetClient) && targetClient.PlayerObject != null) {
            var s = targetClient.PlayerObject.GetComponent<PlayerStats>();
            s?.ApplyDamage(a, p, i);
            SyncHpClientRpc(targetClient.PlayerObject.NetworkObjectId, s.CurrentHp, s.maxHp);
        }
    }

    [ClientRpc] 
    private void SyncHpClientRpc(ulong id, int h, int m) {
        if(NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var o)) 
            o.GetComponent<PlayerStats>()?.ClientSyncHp(h, m);
    }

    private void Update()
    {
        UpdateVisualBeam(); // Corre em TODOS os clientes

        if (!IsServer || !GameManager.Instance.isP2P || GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        var downedList = playerAliveState.Where(x => !x.Value).Select(x => x.Key).ToList();
        bool anyoneReviving = false;

        foreach (var downedId in downedList)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(downedId, out var downedClient)) continue;
            if (downedClient.PlayerObject == null) continue;
            
            bool rescuerNear = false;
            ulong rescuerId = 0;

            foreach (var rescuer in NetworkManager.Singleton.ConnectedClientsList) {
                if (rescuer.ClientId == downedId) continue;
                if (!playerAliveState.GetValueOrDefault(rescuer.ClientId, true)) continue;
                if (rescuer.PlayerObject == null) continue;
                
                if (Vector3.Distance(downedClient.PlayerObject.transform.position, rescuer.PlayerObject.transform.position) <= reviveRadius) {
                    rescuerNear = true; rescuerId = rescuer.ClientId; break;
                }
            }

            if (rescuerNear) {
                float p = reviveProgress.GetValueOrDefault(downedId, 0f) + Time.deltaTime;
                reviveProgress[downedId] = p;
                activeReviveLink.Value = new Vector2Int((int)rescuerId, (int)downedId); // Liga o visual
                anyoneReviving = true;

                if (p >= reviveTime) {
                    playerAliveState[downedId] = true; reviveProgress.Remove(downedId);
                    var ps = downedClient.PlayerObject.GetComponent<PlayerStats>();
                    if(ps){ 
                        ps.ServerReviveToFixedHp(10); 
                        ps.SetDownedState(false); 
                        SyncHpClientRpc(downedClient.PlayerObject.NetworkObjectId, 10, ps.maxHp); 
                    }
                    PlayReviveVFXClientRpc(downedClient.PlayerObject.NetworkObjectId);
                    activeReviveLink.Value = new Vector2Int(-1, -1);
                }
            } else {
                if (reviveProgress.ContainsKey(downedId)) reviveProgress[downedId] = 0f;
            }
        }

        if (!anyoneReviving && activeReviveLink.Value.x != -1) activeReviveLink.Value = new Vector2Int(-1, -1);
        CheckGameOverCondition();
    }

    private void UpdateVisualBeam()
    {
        if (reviveBeam == null) return;
        Vector2Int link = activeReviveLink.Value;
        
        // Se o link for inválido, desliga
        if (link.x == -1 || link.y == -1) { 
            reviveBeam.enabled = false; 
            return; 
        }

        var nm = NetworkManager.Singleton;
        // Verifica se ambos os jogadores existem
        if (nm!=null && nm.ConnectedClients.TryGetValue((ulong)link.x, out var r) && nm.ConnectedClients.TryGetValue((ulong)link.y, out var d)) {
            if (r.PlayerObject && d.PlayerObject) {
                reviveBeam.enabled = true;
                // + Vector3.up para levantar o raio do chão
                reviveBeam.SetPosition(0, r.PlayerObject.transform.position + Vector3.up);
                reviveBeam.SetPosition(1, d.PlayerObject.transform.position + Vector3.up);
                return;
            }
        }
        reviveBeam.enabled = false;
    }

    private void CheckGameOverCondition() {
        if (playerAliveState.Count > 0 && !playerAliveState.Values.Any(x => x)) GameManager.Instance.GameOver();
    }

    [ClientRpc] 
    private void PlayReviveVFXClientRpc(ulong id) {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var o)) {
            if(reviveSprite){ 
                GameObject v = new GameObject("VFX"); 
                v.transform.position = o.transform.position; 
                var sr = v.AddComponent<SpriteRenderer>(); 
                sr.sprite = reviveSprite; 
                sr.color = reviveColor;
                // Faz o sprite olhar para a câmara se for 3D
                v.transform.rotation = Camera.main.transform.rotation;
                Destroy(v, 1f); 
            }
        }
    }
}