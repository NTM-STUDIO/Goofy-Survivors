using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class LobbyManagerP2P : NetworkBehaviour
{
    [Header("UI Prefab")]
    [Tooltip("O prefab visual da UI (NÃO DEVE ter NetworkObject)")]
    public GameObject lobbyUiPrefab;
    
    [Header("Game Data")]
    public List<GameObject> unitPrefabs; // Lista central de unidades possíveis

    // Referência para a UI local instanciada
    private LobbyUI localUI;

    // Dicionário para o Host saber as seleções atuais de todos
    private Dictionary<ulong, int> serverPlayerSelections = new Dictionary<ulong, int>();

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);

        // 1. Encontrar o Canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("LobbyManagerP2P: Não foi encontrado nenhum Canvas!");
            return;
        }

        // 2. Instanciar a UI localmente dentro do Canvas
        GameObject uiObj = Instantiate(lobbyUiPrefab, mainCanvas.transform);
        localUI = uiObj.GetComponent<LobbyUI>();
        
        // 3. Inicializar a UI
        localUI.Initialize(this, IsHost, GetLocalIPAddress());

        // 4. Configurar eventos de rede
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // Adiciona o Host imediatamente
            OnClientConnected(NetworkManager.Singleton.LocalClientId);
        }
    }

    // --- Lógica do SERVIDOR ---

    private void OnClientConnected(ulong clientId)
    {
        if (!serverPlayerSelections.ContainsKey(clientId))
        {
            serverPlayerSelections.Add(clientId, 0); // Seleção inicial 0
        }

        // Atualiza TODOS os clientes sobre o novo jogador e vice-versa
        RefreshLobbyForAllClientRpc();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (serverPlayerSelections.ContainsKey(clientId))
        {
            serverPlayerSelections.Remove(clientId);
        }
        RefreshLobbyForAllClientRpc();
    }

    [ClientRpc]
    private void RefreshLobbyForAllClientRpc()
    {
        // Este método é um pouco "bruto", mas garante sincronia. 
        // O servidor manda todos atualizarem a lista completa.
        if (!IsServer) return; // Só o servidor comanda esta atualização

        // Prepara os dados para enviar (quem está conectado e o que selecionou)
        ulong[] ids = new ulong[serverPlayerSelections.Count];
        int[] selections = new int[serverPlayerSelections.Count];
        int i = 0;
        foreach(var kvp in serverPlayerSelections)
        {
            ids[i] = kvp.Key;
            selections[i] = kvp.Value;
            i++;
        }

        // Envia os dados para os clientes renderizarem
        SyncLobbyStateClientRpc(ids, selections);
    }

    [ClientRpc]
    private void SyncLobbyStateClientRpc(ulong[] connectedIds, int[] currentSelections)
    {
        // Limpa slots antigos (simplificação para garantir sincronia)
        // Numa implementação mais avançada, faríamos diffs, mas isto funciona.
        // NOTA: Se isto causar "piscar" na UI, precisamos de uma lógica de "diff" melhor.
        // Por agora, vamos assumir que o LobbyUI sabe lidar com duplicados no AddPlayerSlot.
        
        // Para simplificar, vamos só adicionar quem falta e atualizar seleções.
        for (int i = 0; i < connectedIds.Length; i++)
        {
            ulong uid = connectedIds[i];
            int sel = currentSelections[i];
            string pName = uid == NetworkManager.Singleton.LocalClientId ? "You" : $"Player {uid}";
            bool isLocal = uid == NetworkManager.Singleton.LocalClientId;

            // Tenta adicionar (o UI ignora se já existir)
            localUI.AddPlayerSlot(uid, pName, isLocal, sel, unitPrefabs);
            // Atualiza a seleção
            localUI.UpdatePlayerSelection(uid, sel);
        }

        // (Opcional) Lógica para remover jogadores que desconectaram da UI local seria adicionada aqui
    }

    // --- Comunicação UI -> Servidor ---

    public void LocalPlayerChangedSelection(int newIndex)
    {
        // Chamado pela UI local quando o jogador clica Next/Prev
        SubmitSelectionServerRpc(newIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitSelectionServerRpc(int newIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (serverPlayerSelections.ContainsKey(senderId))
        {
            serverPlayerSelections[senderId] = newIndex;
            // Informa todos da mudança
            RefreshLobbyForAllClientRpc();
        }
    }

    public void OnStartGameClicked()
    {
        // Recolhe os prefabs finais
        Dictionary<ulong, GameObject> finalSelections = new Dictionary<ulong, GameObject>();
        foreach(var kvp in serverPlayerSelections)
        {
            finalSelections.Add(kvp.Key, unitPrefabs[kvp.Value]);
        }

        GameManager.Instance.SetPlayerSelections_P2P(finalSelections);
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        if (localUI != null) Destroy(localUI.gameObject); // Destroi a UI
        GameManager.Instance.StartGame();
        gameObject.SetActive(false); // Desativa este manager
    }
    
    // --- Helpers ---
    private string GetLocalIPAddress() { /* (O seu código de IP aqui) */ return "127.0.0.1"; }

    public override void OnNetworkDespawn()
    {
        if (localUI != null) Destroy(localUI.gameObject);
        base.OnNetworkDespawn();
    }
}