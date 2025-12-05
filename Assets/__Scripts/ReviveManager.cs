using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class ReviveManager : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float reviveRadius = 3.0f;
    
    // --- MUDANÇA AQUI: Tempo padrão agora é 1 segundo ---
    [SerializeField] private float reviveTime = 1.0f; 

    [Header("Visuals")]
    [SerializeField] private LineRenderer reviveBeam; // Arraste o LineRenderer aqui
    [SerializeField] private Sprite reviveSprite;
    [SerializeField] private Color reviveColor = Color.green;

    private Dictionary<ulong, bool> playerAliveState = new Dictionary<ulong, bool>();
    private Dictionary<ulong, float> reviveProgress = new Dictionary<ulong, float>();

    // Variável para sincronizar o feixe visual (Quem está a salvar -> Quem)
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

    public void ResetReviveState()
    {
        playerAliveState.Clear();
        reviveProgress.Clear();
        if (IsServer) activeReviveLink.Value = new Vector2Int(-1, -1);
    }

    public void NotifyPlayerDowned(ulong clientId)
    {
        if (!GameManager.Instance.isP2P) { GameManager.Instance.GameOver(); return; }

        if (IsServer)
        {
            foreach(var client in NetworkManager.Singleton.ConnectedClientsList) 
                if(!playerAliveState.ContainsKey(client.ClientId)) playerAliveState[client.ClientId] = true;

            playerAliveState[clientId] = false;
            reviveProgress[clientId] = 0f;
            
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var downedClient) && downedClient.PlayerObject != null)
            {
                var stats = downedClient.PlayerObject.GetComponent<PlayerStats>();
                if (stats != null) stats.SetDownedState(true);
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

    public void ServerApplyPlayerDamage(ulong targetId, float amount, Vector3? pos, float? iframe)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out var targetClient) && targetClient.PlayerObject != null)
        {
            var stats = targetClient.PlayerObject.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.ApplyDamage(amount, pos, iframe);
                SyncHpClientRpc(targetClient.PlayerObject.NetworkObjectId, stats.CurrentHp, stats.maxHp);
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
                    // REVIVE COMPLETO!
                    playerAliveState[downedId] = true;
                    reviveProgress.Remove(downedId);
                    
                    var ps = downedClient.PlayerObject.GetComponent<PlayerStats>();
                    if (ps != null) 
                    {
                        ps.ServerReviveToFixedHp(10);
                        ps.SetDownedState(false); 
                        SyncHpClientRpc(downedClient.PlayerObject.NetworkObjectId, 10, ps.maxHp);
                    }
                    
                    PlayReviveVFXClientRpc(downedClient.PlayerObject.NetworkObjectId);
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
    private void PlayReviveVFXClientRpc(ulong netId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var obj))
        {
            if (reviveSprite != null)
            {
                GameObject vfx = new GameObject("ReviveVFX");
                vfx.transform.position = obj.transform.position;
                var sr = vfx.AddComponent<SpriteRenderer>();
                sr.sprite = reviveSprite;
                sr.color = reviveColor;
                
                // Faz o sprite olhar para a câmara (Billboarding)
                if (Camera.main != null)
                    vfx.transform.rotation = Camera.main.transform.rotation;

                Destroy(vfx, 1.0f);
                StartCoroutine(AnimateVFX(vfx.transform, sr));
            }
        }
    }

    private System.Collections.IEnumerator AnimateVFX(Transform t, SpriteRenderer sr)
    {
        float timer = 0f;
        while (timer < 1f)
        {
            if (t == null) yield break;
            timer += Time.deltaTime;
            t.localScale = Vector3.one * (1f + timer); 
            Color c = sr.color;
            c.a = 1f - timer; 
            sr.color = c;
            yield return null;
        }
    }
}